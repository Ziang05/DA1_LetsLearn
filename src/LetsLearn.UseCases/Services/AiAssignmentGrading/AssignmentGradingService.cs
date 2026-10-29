using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Configuration;

namespace LetsLearn.UseCases.Services.AiAssignmentGrading
{
    public sealed class AssignmentGradingService : IAssignmentGradingService
    {
        private const string ModelConfigKey = "AssignmentGradingModel";
        private readonly IAiLlmClient _llmClient;
        private readonly AssignmentGradingPromptBuilder _promptBuilder;
        private readonly AssignmentGradingResultValidator _resultValidator;
        private readonly IConfiguration _configuration;

        public AssignmentGradingService(
            IAiLlmClient llmClient,
            AssignmentGradingPromptBuilder promptBuilder,
            AssignmentGradingResultValidator resultValidator,
            IConfiguration configuration)
        {
            _llmClient = llmClient;
            _promptBuilder = promptBuilder;
            _resultValidator = resultValidator;
            _configuration = configuration;
        }

        public async Task<ValidatedAssignmentGrade> GradeAsync(
            AssignmentGradingInput input,
            AssignmentRubric rubric,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(rubric);
            ValidateInputSnapshot(input, rubric);

            var configuredPromptVersion = _configuration["AIGrading:PromptVersion"] ?? "grading-v1";
            if (!string.Equals(input.PromptVersion, configuredPromptVersion, StringComparison.Ordinal))
            {
                throw new AssignmentGradingException("unsupported_prompt_version", "Phiên bản prompt của job không được hỗ trợ bởi phiên bản ứng dụng hiện tại.");
            }

            var maxTextLength = GetPositiveSetting("AIGrading:MaxExtractedTextLength", 100_000);
            if (input.SubmissionText.Length == 0 || input.SubmissionText.Length > maxTextLength)
            {
                throw new AssignmentGradingException("invalid_submission_text", "Bản chụp văn bản bài nộp rỗng hoặc vượt giới hạn.");
            }

            var model = input.Model.Trim();
            var timeoutSeconds = Math.Clamp(GetPositiveSetting("AIGrading:ProviderTimeoutSeconds", 60), 1, 60);
            var prompt = _promptBuilder.Build(input, rubric);
            string? response;
            try
            {
                response = await _llmClient.GenerateJsonWithOptionsAsync(
                    prompt.SystemPrompt,
                    prompt.UserPrompt,
                    temperature: 0.1m,
                    ct: ct,
                    modelConfigKey: ModelConfigKey,
                    requestTimeout: TimeSpan.FromSeconds(timeoutSeconds),
                    modelOverride: model);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new AssignmentGradingException("provider_timeout", "Nhà cung cấp AI vượt quá thời gian xử lý cho phép.", ex);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("returned 429", StringComparison.OrdinalIgnoreCase))
            {
                throw new AssignmentGradingException("provider_rate_limited", "Nhà cung cấp AI đang giới hạn yêu cầu; job có thể được thử lại theo chính sách queue.", ex);
            }
            catch (InvalidOperationException ex)
            {
                var statusCode = TryGetProviderStatusCode(ex.Message);
                var code = statusCode is 408 or 425 || statusCode >= 500
                    ? "provider_error"
                    : "provider_rejected_request";
                throw new AssignmentGradingException(code, "Nhà cung cấp AI từ chối hoặc không thể hoàn tất yêu cầu chấm.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new AssignmentGradingException("provider_error", "Không thể kết nối tới nhà cung cấp AI.", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new AssignmentGradingException("provider_error", "Nhà cung cấp AI không thể hoàn tất yêu cầu chấm.", ex);
            }

            if (string.IsNullOrWhiteSpace(response))
            {
                throw new AssignmentGradingException("provider_not_configured", "Nhà cung cấp AI chưa được cấu hình hoặc không trả kết quả.");
            }

            return _resultValidator.Validate(response, input.SubmissionText, rubric);
        }

        private static void ValidateInputSnapshot(AssignmentGradingInput input, AssignmentRubric rubric)
        {
            if (input.AssignmentResponseId == Guid.Empty
                || input.RubricId != rubric.Id
                || input.RubricVersion != rubric.Version
                || string.IsNullOrWhiteSpace(input.PromptVersion)
                || string.IsNullOrWhiteSpace(input.Model)
                || !string.Equals(input.RubricJson, AssignmentGradingInputHasher.SerializeRubric(rubric), StringComparison.Ordinal))
            {
                throw new AssignmentGradingException("grading_snapshot_mismatch", "Thông tin job không khớp snapshot rubric/prompt/model đã lưu.");
            }
        }

        private int GetPositiveSetting(string key, int defaultValue) =>
            int.TryParse(_configuration[key], out var value) && value > 0 ? value : defaultValue;

        private static int? TryGetProviderStatusCode(string message)
        {
            var marker = message.IndexOf("returned ", StringComparison.OrdinalIgnoreCase);
            return marker >= 0
                && message.Length >= marker + 12
                && int.TryParse(message.AsSpan(marker + 9, 3), out var statusCode)
                    ? statusCode
                    : null;
        }
    }
}
