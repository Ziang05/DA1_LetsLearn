using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LetsLearn.UseCases.Services.AiQuestionGeneration
{
    public class PgvectorAiVectorStore : IAiVectorStore
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PgvectorAiVectorStore> _logger;

        public PgvectorAiVectorStore(IConfiguration configuration, ILogger<PgvectorAiVectorStore> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task UpsertChunkEmbeddingsAsync(IEnumerable<LectureChunkVector> chunks, CancellationToken ct = default)
        {
            var chunkList = chunks.Where(c => c.Embedding.Length > 0).ToList();
            if (chunkList.Count == 0)
            {
                _logger.LogInformation("[AI Questions] pgvector upsert skipped because no chunk embeddings were available.");
                return;
            }

            try
            {
                await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync(ct);

                foreach (var chunk in chunkList)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = @"
                        UPDATE ""LectureChunks""
                        SET ""EmbeddingVector"" = CAST(@embedding AS vector)
                        WHERE ""Id"" = @id;";
                    command.Parameters.AddWithValue("embedding", ToPgvectorLiteral(chunk.Embedding));
                    command.Parameters.AddWithValue("id", chunk.ChunkId);
                    await command.ExecuteNonQueryAsync(ct);
                }

                _logger.LogInformation("[AI Questions] pgvector upsert completed. ChunkCount={ChunkCount}, Dimensions={Dimensions}", chunkList.Count, chunkList.First().Embedding.Length);
            }
            catch (Exception ex)
            {
                // Keep JSON embeddings as a functional fallback when pgvector is not installed yet.
                _logger.LogWarning(ex, "[AI Questions] pgvector upsert failed; JSON embeddings remain available as fallback. ChunkCount={ChunkCount}", chunkList.Count);
            }
        }

        public async Task<List<Guid>> SearchSimilarChunksAsync(Guid documentId, float[] queryEmbedding, int topK, CancellationToken ct = default)
        {
            if (queryEmbedding.Length == 0)
            {
                _logger.LogInformation("[AI Questions] pgvector search skipped because query embedding is empty. DocumentId={DocumentId}", documentId);
                return new List<Guid>();
            }

            try
            {
                await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync(ct);

                await using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT ""Id""
                    FROM ""LectureChunks""
                    WHERE ""DocumentId"" = @documentId
                      AND ""EmbeddingVector"" IS NOT NULL
                    ORDER BY ""EmbeddingVector"" <=> CAST(@embedding AS vector)
                    LIMIT @topK;";
                command.Parameters.AddWithValue("documentId", documentId);
                command.Parameters.AddWithValue("embedding", ToPgvectorLiteral(queryEmbedding));
                command.Parameters.AddWithValue("topK", Math.Clamp(topK, 1, 12));

                var result = new List<Guid>();
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    result.Add(reader.GetGuid(0));
                }

                _logger.LogInformation("[AI Questions] pgvector search completed. DocumentId={DocumentId}, TopK={TopK}, ResultCount={ResultCount}, Dimensions={Dimensions}", documentId, topK, result.Count, queryEmbedding.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AI Questions] pgvector search failed; retrieval will fall back. DocumentId={DocumentId}, TopK={TopK}", documentId, topK);
                return new List<Guid>();
            }
        }

        private static string ToPgvectorLiteral(float[] vector)
        {
            return $"[{string.Join(",", vector.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))}]";
        }
    }
}
