using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class QuizResponseRequest
    {
        public Guid TopicId { get; set; }
        public QuizResponseData Data { get; set; } = new();
    }

    public class QuizResponseData
    {
        public string? Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<QuizResponseAnswerDTO> Answers { get; set; } = new();
    }

    public class QuizResponseAnswerDTO
    {
        public Guid TopicQuizQuestionId { get; set; }
        public QuestionDTO Question { get; set; } = new();
        public string? Answer { get; set; } = "";
        public decimal? Mark { get; set; }
    }

    public class QuestionDTO
    {
        public Guid Id { get; set; }
        public string? Type { get; set; }
        public string? QuestionText { get; set; }
        public decimal? DefaultMark { get; set; }
        public QuestionDataDTO Data { get; set; } = new();
    }

    public class QuestionDataDTO
    {
        // For Choice Questions
        public bool Multiple { get; set; }
        public List<QuestionChoiceDTO> Choices { get; set; } = new();

        // For True/False Questions
        public bool? CorrectAnswer { get; set; }
        public string? FeedbackOfTrue { get; set; }
        public string? FeedbackOfFalse { get; set; }
    }

    public class QuestionChoiceDTO
    {
        public string Id { get; set; } = "";
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }

    public class QuizResponseDTO
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public Guid TopicId { get; set; }

        public QuizResponseData Data { get; set; } = new();
    }
}