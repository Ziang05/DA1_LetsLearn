using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;

namespace LetsLearn.UseCases.Services.AiAssignmentGrading
{
    public sealed class AssignmentGradingResultValidator
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            MaxDepth = 32
        };

        private static readonly HashSet<string> ResultProperties = new(StringComparer.Ordinal)
        {
            "criterionResults", "summaryFeedback", "strengths", "improvements", "studySuggestions"
        };
        private static readonly HashSet<string> CriterionProperties = new(StringComparer.Ordinal)
        {
            "criterionId", "score", "feedback", "evidence"
        };
        private static readonly HashSet<string> EvidenceProperties = new(StringComparer.Ordinal)
        {
            "quote", "page"
        };

        public ValidatedAssignmentGrade Validate(
            string? json,
            string normalizedSubmissionText,
            AssignmentRubric rubric)
        {
            ArgumentNullException.ThrowIfNull(normalizedSubmissionText);
            ArgumentNullException.ThrowIfNull(rubric);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw InvalidResult("provider_empty_response", "Nhà cung cấp AI không trả về kết quả chấm.");
            }
            if (json.Length > 512_000)
            {
                throw InvalidResult("provider_response_too_large", "Phản hồi AI vượt giới hạn kích thước cho phép.");
            }

            AssignmentGradingLlmResult result;
            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
                ValidateObjectShape(document.RootElement, ResultProperties, "result");
                var criterionArray = document.RootElement.GetProperty("criterionResults");
                if (criterionArray.ValueKind != JsonValueKind.Array)
                {
                    throw InvalidResult("invalid_result_schema", "criterionResults phải là JSON array.");
                }
                foreach (var criterion in criterionArray.EnumerateArray())
                {
                    ValidateObjectShape(criterion, CriterionProperties, "criterion result");
                    if (criterion.TryGetProperty("evidence", out var evidence) && evidence.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in evidence.EnumerateArray())
                        {
                            ValidateObjectShape(item, EvidenceProperties, "evidence");
                        }
                    }
                }
                result = JsonSerializer.Deserialize<AssignmentGradingLlmResult>(json, JsonOptions)
                    ?? throw InvalidResult("invalid_result_schema", "JSON kết quả chấm không hợp lệ.");
            }
            catch (AssignmentGradingException)
            {
                throw;
            }
            catch (JsonException ex)
            {
                throw InvalidResult("invalid_result_json", "AI trả về JSON sai định dạng.", ex);
            }

            var criteria = rubric.Criteria.OrderBy(item => item.SortOrder).ThenBy(item => item.Id).ToList();
            if (rubric.MaxScore != 10m || criteria.Count == 0
                || criteria.Any(item => item.MaxScore <= 0m)
                || criteria.Sum(item => item.MaxScore) != rubric.MaxScore)
            {
                throw InvalidResult("invalid_rubric", "Rubric không hợp lệ: tổng điểm tối đa của tiêu chí phải bằng điểm tối đa của rubric.");
            }

            if (result.CriterionResults == null || result.CriterionResults.Count != criteria.Count)
            {
                throw InvalidResult("criterion_set_mismatch", "Kết quả thiếu hoặc thừa tiêu chí so với rubric.");
            }
            if (result.CriterionResults.Select(item => item.CriterionId).Distinct().Count() != result.CriterionResults.Count)
            {
                throw InvalidResult("duplicate_criterion", "Kết quả có tiêu chí bị lặp.");
            }

            var rubricById = criteria.ToDictionary(item => item.Id);
            decimal total = 0m;
            foreach (var item in result.CriterionResults)
            {
                if (!rubricById.TryGetValue(item.CriterionId, out var criterion))
                {
                    throw InvalidResult("unknown_criterion", "Kết quả chứa criterion ID không thuộc rubric hiện tại.");
                }
                if (!item.Score.HasValue || item.Score.Value < 0m || item.Score.Value > criterion.MaxScore)
                {
                    throw InvalidResult("criterion_score_out_of_range", "Điểm tiêu chí bị thiếu, âm hoặc vượt điểm tối đa.");
                }
                ValidateText(item.Feedback, "criterion feedback", 2000, required: true);
                if (item.Evidence == null || item.Evidence.Count > 20)
                {
                    throw InvalidResult("invalid_evidence", "Danh sách evidence không hợp lệ.");
                }
                foreach (var evidence in item.Evidence)
                {
                    ValidateText(evidence.Quote, "evidence quote", 1000, required: true);
                    if (!normalizedSubmissionText.Contains(evidence.Quote!, StringComparison.Ordinal))
                    {
                        throw InvalidResult("evidence_not_in_submission", "Evidence quote không xuất hiện nguyên văn trong bài nộp đã chuẩn hóa.");
                    }
                    if (evidence.Page is <= 0)
                    {
                        throw InvalidResult("invalid_evidence_page", "Số trang evidence phải lớn hơn 0 hoặc để trống.");
                    }
                }
                total += item.Score.Value;
            }

            ValidateText(result.SummaryFeedback, "summary feedback", 4000, required: true);
            ValidateTextList(result.Strengths, "strengths");
            ValidateTextList(result.Improvements, "improvements");
            ValidateTextList(result.StudySuggestions, "studySuggestions");

            var sortedResults = result.CriterionResults.OrderBy(item => rubricById[item.CriterionId].SortOrder)
                .ThenBy(item => item.CriterionId).ToList();
            return new ValidatedAssignmentGrade
            {
                SuggestedScore = decimal.Round(total, 2, MidpointRounding.AwayFromZero),
                CriterionResultsJson = JsonSerializer.Serialize(sortedResults, JsonOptions),
                SummaryFeedback = result.SummaryFeedback!.Trim(),
                StrengthsJson = JsonSerializer.Serialize(result.Strengths, JsonOptions),
                ImprovementsJson = JsonSerializer.Serialize(result.Improvements, JsonOptions),
                StudySuggestionsJson = JsonSerializer.Serialize(result.StudySuggestions, JsonOptions)
            };
        }

        private static void ValidateObjectShape(JsonElement element, HashSet<string> allowedProperties, string objectName)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw InvalidResult("invalid_result_schema", $"{objectName} phải là JSON object.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!allowedProperties.Contains(property.Name) || !seen.Add(property.Name))
                {
                    throw InvalidResult("invalid_result_schema", $"{objectName} có field lạ hoặc bị lặp: {property.Name}.");
                }
            }
            if (allowedProperties.Any(property => !seen.Contains(property)))
            {
                throw InvalidResult("invalid_result_schema", $"{objectName} thiếu field bắt buộc.");
            }
        }

        private static void ValidateTextList(List<string>? values, string fieldName)
        {
            if (values == null || values.Count > 20)
            {
                throw InvalidResult("invalid_result_text", $"Danh sách {fieldName} không hợp lệ.");
            }
            foreach (var value in values)
            {
                ValidateText(value, fieldName, 1000, required: true);
            }
        }

        private static void ValidateText(string? value, string fieldName, int maxLength, bool required)
        {
            if ((required && string.IsNullOrWhiteSpace(value)) || value?.Length > maxLength || value?.Contains('\0') == true)
            {
                throw InvalidResult("invalid_result_text", $"Trường {fieldName} rỗng hoặc vượt giới hạn độ dài.");
            }
        }

        private static AssignmentGradingException InvalidResult(string code, string message, Exception? inner = null) =>
            new(code, message, inner);
    }
}
