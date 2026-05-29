using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LetsLearn.UseCases.Services.AiQuestionGeneration
{
    public class AiLlmClient : IAiLlmClient
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiLlmClient> _logger;

        public AiLlmClient(IConfiguration configuration, ILogger<AiLlmClient> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> GenerateJsonAsync(string systemPrompt, string userPrompt, decimal temperature, CancellationToken ct = default, string modelConfigKey = "GeneratorModel")
        {
            var provider = _configuration["AI:Provider"] ?? "Gemini";
            if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                return await GenerateGeminiJsonAsync(systemPrompt, userPrompt, temperature, modelConfigKey, ct);
            }

            return await GenerateOpenAiCompatibleJsonAsync(systemPrompt, userPrompt, temperature, modelConfigKey, ct);
        }

        private async Task<string?> GenerateGeminiJsonAsync(string systemPrompt, string userPrompt, decimal temperature, string modelConfigKey, CancellationToken ct)
        {
            var apiKey = _configuration["AI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("[AI Questions] AI:ApiKey is empty; LLM generation will use fallback behavior. ModelConfigKey={ModelConfigKey}", modelConfigKey);
                return null;
            }

            var baseUrl = _configuration["AI:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
            var model = _configuration[$"AI:{modelConfigKey}"] ?? _configuration["AI:GeneratorModel"] ?? "gemini-2.5-flash";
            _logger.LogInformation("[AI Questions] Calling Gemini generateContent. Model={Model}, ModelConfigKey={ModelConfigKey}, Temperature={Temperature}", model, modelConfigKey, temperature);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

            var payload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userPrompt } }
                    }
                },
                generationConfig = new
                {
                    temperature,
                    responseMimeType = "application/json"
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            const int maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                // Cần tạo mới content mỗi lần vì HttpContent chỉ dùng được 1 lần
                using var requestContent = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}", requestContent, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(responseJson);
                    var text = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();
                    _logger.LogInformation("[AI Questions] Gemini generateContent succeeded. Model={Model}, ResponseChars={ResponseChars}", model, text?.Length ?? 0);
                    return text;
                }

                var error = await response.Content.ReadAsStringAsync(ct);

                // 429 Rate limit → thử retry sau khoảng thời gian Gemini yêu cầu
                if ((int)response.StatusCode == 429 && attempt < maxRetries)
                {
                    var retrySeconds = ParseRetryDelay(error);
                    var waitSeconds = retrySeconds > 0 ? retrySeconds + 2 : 15; // +2s buffer
                    _logger.LogWarning("[AI Questions] Gemini 429 rate limit. Attempt={Attempt}/{Max}, WaitSeconds={WaitSeconds}, Model={Model}", attempt + 1, maxRetries, waitSeconds, model);
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
                    continue;
                }

                _logger.LogError("[AI Questions] Gemini generateContent failed. StatusCode={StatusCode}, Model={Model}, Error={Error}", (int)response.StatusCode, model, error);
                throw new InvalidOperationException($"Gemini returned {(int)response.StatusCode}: {error}");
            }

            throw new InvalidOperationException("Gemini request failed after retries.");
        }

        /// <summary>Đọc retryDelay (giây) từ JSON error response của Gemini 429.</summary>
        private static int ParseRetryDelay(string errorJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(errorJson);
                if (!doc.RootElement.TryGetProperty("error", out var errorEl)) return 0;
                if (!errorEl.TryGetProperty("details", out var details)) return 0;
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.TryGetProperty("@type", out var type) &&
                        type.GetString() == "type.googleapis.com/google.rpc.RetryInfo" &&
                        detail.TryGetProperty("retryDelay", out var delay))
                    {
                        var delayStr = delay.GetString() ?? ""; // e.g. "11s"
                        if (delayStr.EndsWith("s") && int.TryParse(delayStr.TrimEnd('s'), out var seconds))
                            return seconds;
                    }
                }
            }
            catch { /* ignore parse errors */ }
            return 0;
        }

        private async Task<string?> GenerateOpenAiCompatibleJsonAsync(string systemPrompt, string userPrompt, decimal temperature, string modelConfigKey, CancellationToken ct)
        {
            var apiKey = _configuration["AI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("[AI Questions] AI:ApiKey is empty; OpenAI-compatible generation will use fallback behavior. ModelConfigKey={ModelConfigKey}", modelConfigKey);
                return null;
            }

            var baseUrl = _configuration["AI:BaseUrl"] ?? "https://api.openai.com/v1";
            var model = _configuration[$"AI:{modelConfigKey}"] ?? _configuration["AI:GeneratorModel"] ?? "gpt-4o-mini";
            _logger.LogInformation("[AI Questions] Calling OpenAI-compatible chat completions. Model={Model}, ModelConfigKey={ModelConfigKey}, Temperature={Temperature}", model, modelConfigKey, temperature);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model,
                temperature,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/chat/completions", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("[AI Questions] OpenAI-compatible generation failed. StatusCode={StatusCode}, Model={Model}, Error={Error}", (int)response.StatusCode, model, error);
                throw new InvalidOperationException($"AI provider returned {(int)response.StatusCode}: {error}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            _logger.LogInformation("[AI Questions] OpenAI-compatible generation succeeded. Model={Model}, ResponseChars={ResponseChars}", model, text?.Length ?? 0);
            return text;
        }
    }
}
