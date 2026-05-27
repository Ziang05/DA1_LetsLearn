using LetsLearn.UseCases.DTOs;
using Microsoft.AspNetCore.Http;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IAiQuestionGenerationService
    {
        Task<UploadLectureDocumentResponse> UploadDocumentAsync(IFormFile file, string courseId, Guid userId, CancellationToken ct = default);
        Task<GenerateAiQuestionsResponse> GenerateQuestionsAsync(GenerateAiQuestionsRequest request, Guid userId, CancellationToken ct = default);
        Task ProcessGenerationJobAsync(Guid jobId, CancellationToken ct = default);
        Task<GenerateAiQuestionsResponse> GetJobAsync(Guid jobId, Guid userId, CancellationToken ct = default);
        Task<List<AiGeneratedQuestionResponse>> GetGeneratedQuestionsAsync(Guid jobId, Guid userId, CancellationToken ct = default);
        Task<ApproveGeneratedQuestionsResponse> ApproveQuestionsAsync(ApproveGeneratedQuestionsRequest request, Guid userId, CancellationToken ct = default);
    }

    public interface IDocumentTextExtractor
    {
        Task<ExtractedDocument> ExtractAsync(IFormFile file, string savedPath, CancellationToken ct = default);
    }

    public interface IDocumentChunkingService
    {
        List<LectureChunkDto> Chunk(ExtractedDocument document);
    }

    public interface IAiLlmClient
    {
        Task<string?> GenerateJsonAsync(string systemPrompt, string userPrompt, decimal temperature, CancellationToken ct = default, string modelConfigKey = "GeneratorModel");
    }

    public interface IAiEmbeddingClient
    {
        Task<List<float[]?>> CreateEmbeddingsAsync(IEnumerable<string> inputs, CancellationToken ct = default);
    }

    public interface IAiVectorStore
    {
        Task UpsertChunkEmbeddingsAsync(IEnumerable<LectureChunkVector> chunks, CancellationToken ct = default);
        Task<List<Guid>> SearchSimilarChunksAsync(Guid documentId, float[] queryEmbedding, int topK, CancellationToken ct = default);
    }

    public interface IAiQuotaService
    {
        Task CheckAndConsumeAsync(Guid userId, string metric, int amount, int limit, TimeSpan window, CancellationToken ct = default);
    }

    public interface IAiQuestionGenerationJobQueue
    {
        ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default);
        ValueTask<Guid> DequeueAsync(CancellationToken ct = default);
    }

    public class AiQuotaExceededException : Exception
    {
        public AiQuotaExceededException(string message) : base(message)
        {
        }
    }

    public class LectureChunkVector
    {
        public Guid ChunkId { get; set; }
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
