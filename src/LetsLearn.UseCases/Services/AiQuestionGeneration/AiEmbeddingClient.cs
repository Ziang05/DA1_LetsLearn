using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LetsLearn.UseCases.Services.AiQuestionGeneration
{
    public class AiEmbeddingClient : IAiEmbeddingClient
    {
        private const int GeminiMaxBatchSize = 100;

        private readonly IConfiguration _configuration;
        private readonly ILogger<AiEmbeddingClient> _logger;

        public AiEmbeddingClient(IConfiguration configuration, ILogger<AiEmbeddingClient> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<float[]?>> CreateEmbeddingsAsync(IEnumerable<string> inputs, CancellationToken ct = default)
        {
            var inputList = inputs.Select(NormalizeInput).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (inputList.Count == 0)
            {
                _logger.LogInformation("[AI Questions] Embedding skipped because input list is empty.");
                return new List<float[]?>();
            }

            var provider = _configuration["AI:Provider"] ?? "Gemini";
            _logger.LogInformation("[AI Questions] Creating embeddings. Provider={Provider}, InputCount={InputCount}", provider, inputList.Count);
            if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                return await CreateGeminiEmbeddingsAsync(inputList, ct);
            }

            return await CreateOpenAiCompatibleEmbeddingsAsync(inputList, ct);
        }

        private async Task<List<float[]?>> CreateGeminiEmbeddingsAsync(List<string> inputList, CancellationToken ct)
        {
            var apiKey = _configuration["AI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("[AI Questions] AI:ApiKey is empty; embeddings will be null. InputCount={InputCount}", inputList.Count);
                return inputList.Select(_ => (float[]?)null).ToList();
            }

            var baseUrl = _configuration["AI:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
            var model = _configuration["AI:EmbeddingModel"] ?? "gemini-embedding-001";
            var modelName = model.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? model : $"models/{model}";
            var dimensions = GetEmbeddingDimensions();
            _logger.LogInformation("[AI Questions] Calling Gemini batchEmbedContents. Model={Model}, InputCount={InputCount}, Dimensions={Dimensions}", modelName, inputList.Count, dimensions);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            var results = new List<float[]?>(inputList.Count);

            for (var offset = 0; offset < inputList.Count; offset += GeminiMaxBatchSize)
            {
                var batch = inputList
                    .Skip(offset)
                    .Take(GeminiMaxBatchSize)
                    .ToList();

                _logger.LogInformation(
                    "[AI Questions] Calling Gemini embedding batch. Model={Model}, Offset={Offset}, BatchSize={BatchSize}, TotalInputCount={TotalInputCount}",
                    modelName,
                    offset,
                    batch.Count,
                    inputList.Count);

                var payload = new
                {
                    requests = batch.Select(input => new
                    {
                        model = modelName,
                        content = new
                        {
                            parts = new[] { new { text = input } }
                        },
                        embedContentConfig = new
                        {
                            outputDimensionality = dimensions
                        }
                    }).ToArray()
                };

                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/{modelName}:batchEmbedContents?key={Uri.EscapeDataString(apiKey)}", content, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError(
                        "[AI Questions] Gemini embedding failed. StatusCode={StatusCode}, Model={Model}, Offset={Offset}, BatchSize={BatchSize}, Error={Error}",
                        (int)response.StatusCode,
                        modelName,
                        offset,
                        batch.Count,
                        error);
                    throw new InvalidOperationException($"Gemini embedding returned {(int)response.StatusCode}: {error}");
                }

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(responseJson);
                var batchEmbeddings = doc.RootElement
                    .GetProperty("embeddings")
                    .EnumerateArray()
                    .Select(item => (float[]?)item.GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray())
                    .ToList();

                if (batchEmbeddings.Count != batch.Count)
                {
                    _logger.LogWarning(
                        "[AI Questions] Gemini embedding count mismatch. Model={Model}, Offset={Offset}, BatchSize={BatchSize}, EmbeddingCount={EmbeddingCount}",
                        modelName,
                        offset,
                        batch.Count,
                        batchEmbeddings.Count);
                }

                results.AddRange(batchEmbeddings);
            }

            if (results.Count < inputList.Count)
            {
                results.AddRange(Enumerable.Repeat<float[]?>(null, inputList.Count - results.Count));
            }
            else if (results.Count > inputList.Count)
            {
                results = results.Take(inputList.Count).ToList();
            }

            _logger.LogInformation(
                "[AI Questions] Gemini embedding succeeded. Model={Model}, EmbeddingCount={EmbeddingCount}, Dimensions={Dimensions}, BatchSizeLimit={BatchSizeLimit}",
                modelName,
                results.Count,
                results.FirstOrDefault()?.Length ?? 0,
                GeminiMaxBatchSize);
            return results;
        }

        private async Task<List<float[]?>> CreateOpenAiCompatibleEmbeddingsAsync(List<string> inputList, CancellationToken ct)
        {
            var apiKey = _configuration["AI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("[AI Questions] AI:ApiKey is empty; OpenAI-compatible embeddings will be null. InputCount={InputCount}", inputList.Count);
                return inputList.Select(_ => (float[]?)null).ToList();
            }

            var baseUrl = _configuration["AI:BaseUrl"] ?? "https://api.openai.com/v1";
            var model = _configuration["AI:EmbeddingModel"] ?? "text-embedding-3-small";
            _logger.LogInformation("[AI Questions] Calling OpenAI-compatible embeddings. Model={Model}, InputCount={InputCount}", model, inputList.Count);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model,
                input = inputList
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/embeddings", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("[AI Questions] OpenAI-compatible embedding failed. StatusCode={StatusCode}, Model={Model}, Error={Error}", (int)response.StatusCode, model, error);
                throw new InvalidOperationException($"Embedding provider returned {(int)response.StatusCode}: {error}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            var openAiResults = new List<float[]?>();

            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray().OrderBy(e => e.GetProperty("index").GetInt32()))
            {
                var vector = item.GetProperty("embedding")
                    .EnumerateArray()
                    .Select(v => v.GetSingle())
                    .ToArray();
                openAiResults.Add(vector);
            }

            _logger.LogInformation("[AI Questions] OpenAI-compatible embedding succeeded. Model={Model}, EmbeddingCount={EmbeddingCount}, Dimensions={Dimensions}", model, openAiResults.Count, openAiResults.FirstOrDefault()?.Length ?? 0);
            return openAiResults;
        }

        private static string NormalizeInput(string input)
        {
            var normalized = string.Join(" ", input.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries));
            return normalized.Length <= 6000 ? normalized : normalized[..6000];
        }

        private int GetEmbeddingDimensions()
        {
            return int.TryParse(_configuration["AI:EmbeddingDimensions"], out var configured)
                ? configured
                : 768;
        }
    }
}
