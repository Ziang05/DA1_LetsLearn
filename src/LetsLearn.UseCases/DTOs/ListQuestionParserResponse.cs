using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class ListQuestionParserResponse
    {
        public string? QuestionName { get; set; }
        public string? QuestionText { get; set; }
        public string? Type { get; set; }
        public double? DefaultMark { get; set; }
        public bool? CorrectAnswer { get; set; }
        public bool Multiple { get; set; }
        public List<ListQuestionChoiceResponse>? Choices { get; set; }
    }

    public class ListQuestionChoiceResponse
    {
        public string? Text { get; set; }
        public decimal GradePercent { get; set; }
        public string? Feedback { get; set; }
    }
}
