using LetsLearn.Core.Entities;

namespace LetsLearn.UseCases.Services
{
    /// <summary>
    /// Recalculates the objective quiz score from the server-stored question snapshot
    /// and the answer encoding used by the learner client.
    /// </summary>
    public static class QuizAnswerScorer
    {
        public static decimal? CalculateMark(Question question, string? answer)
        {
            if (question.DefaultMark is not decimal maxMark || maxMark <= 0m)
            {
                return null;
            }

            answer ??= string.Empty;
            if (answer.Length == 0)
            {
                return 0m;
            }

            var type = question.Type?.Trim().ToLowerInvariant();
            if (type is "true/false" or "truefalse" or "boolean")
            {
                if (answer is not ("0" or "1")) return null;
                return (answer == "1") == question.CorrectAnswer ? maxMark : 0m;
            }

            var choices = question.Choices?.ToList() ?? new List<QuestionChoice>();
            if (type is "short answer" or "shortanswer" or "text")
            {
                var match = choices.FirstOrDefault(choice =>
                    string.Equals(choice.Text?.Trim(), answer.Trim(), StringComparison.OrdinalIgnoreCase));
                return match?.GradePercent is decimal percent
                    ? RoundMark(maxMark * percent / 100m)
                    : 0m;
            }

            if (type is not ("choices answer" or "choice" or "multiple choice") || choices.Count == 0)
            {
                return null;
            }

            if (question.Multiple)
            {
                if (answer.Length != choices.Count || answer.Any(character => character is not ('0' or '1')))
                {
                    return null;
                }

                var percent = choices
                    .Where((_, index) => answer[index] == '1')
                    .Sum(choice => choice.GradePercent ?? 0m);
                return RoundMark(maxMark * percent / 100m);
            }

            if (!int.TryParse(answer, out var choiceIndex) || choiceIndex < 0 || choiceIndex >= choices.Count)
            {
                return null;
            }

            return RoundMark(maxMark * (choices[choiceIndex].GradePercent ?? 0m) / 100m);
        }

        private static decimal RoundMark(decimal mark) =>
            Math.Round(mark, 0, MidpointRounding.AwayFromZero);
    }
}
