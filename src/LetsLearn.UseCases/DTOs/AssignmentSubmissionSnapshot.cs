namespace LetsLearn.UseCases.DTOs
{
    public sealed class AssignmentSubmissionSnapshot
    {
        public string Text { get; init; } = string.Empty;
        public string ContentHash { get; init; } = string.Empty;
        public int FileCount { get; init; }
    }

    public sealed class AssignmentGradingInput
    {
        public Guid AssignmentResponseId { get; init; }
        public string SubmissionText { get; init; } = string.Empty;
        public string AssignmentDescription { get; init; } = string.Empty;
        public Guid RubricId { get; init; }
        public int RubricVersion { get; init; }
        public string RubricJson { get; init; } = string.Empty;
        public string PromptVersion { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
    }

    public static class AssignmentGradingInputHasher
    {
        public static string SerializeRubric(LetsLearn.Core.Entities.AssignmentRubric rubric)
        {
            ArgumentNullException.ThrowIfNull(rubric);
            var snapshot = new
            {
                rubric.Id,
                rubric.Version,
                rubric.MaxScore,
                TeacherGuidance = rubric.TeacherGuidance ?? string.Empty,
                Criteria = rubric.Criteria.OrderBy(criterion => criterion.SortOrder).ThenBy(criterion => criterion.Id)
                    .Select(criterion => new
                    {
                        criterion.Id,
                        criterion.Name,
                        criterion.Description,
                        criterion.MaxScore,
                        criterion.SortOrder
                    })
            };
            return System.Text.Json.JsonSerializer.Serialize(snapshot);
        }

        public static string ComputeSubmissionHash(string normalizedSubmissionText)
        {
            ArgumentNullException.ThrowIfNull(normalizedSubmissionText);
            return Hash(normalizedSubmissionText);
        }

        public static string ComputeRequestHash(
            LetsLearn.Core.Entities.AssignmentResponse submission,
            string assignmentDescription,
            string rubricJson,
            string promptVersion,
            string model)
        {
            ArgumentNullException.ThrowIfNull(submission);
            var values = new List<string>
            {
                submission.Id.ToString("D"),
                submission.SubmittedAt?.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                assignmentDescription,
                rubricJson,
                promptVersion,
                model
            };
            values.AddRange(submission.Files.OrderBy(file => file.Id).Select(file =>
                $"{file.Id:D}|{file.Name ?? string.Empty}|{file.DownloadUrl ?? string.Empty}"));
            return Hash(string.Concat(values.Select(value => $"{value.Length}:{value}")));
        }

        public static string ComputeInputHash(AssignmentGradingInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            // Length-prefix every component so delimiter characters inside document text
            // cannot make two distinct grading inputs produce the same canonical payload.
            var values = new[]
            {
                input.AssignmentResponseId.ToString("D"),
                input.SubmissionText,
                input.AssignmentDescription,
                input.RubricId.ToString("D"),
                input.RubricVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                input.RubricJson,
                input.PromptVersion,
                input.Model
            };
            var canonical = string.Concat(values.Select(value => $"{value.Length}:{value}"));
            return Hash(canonical);
        }

        private static string Hash(string value) =>
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    public sealed class AssignmentSubmissionExtractionException : Exception
    {
        public AssignmentSubmissionExtractionException(string code, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
