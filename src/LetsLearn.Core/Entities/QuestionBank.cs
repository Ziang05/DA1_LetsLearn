using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class Question
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? QuestionName { get; set; }
        public string? QuestionText { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
        public decimal? DefaultMark { get; set; }
        public long? Usage { get; set; }
        public string? FeedbackOfTrue { get; set; }
        public string? FeedbackOfFalse { get; set; }
        public bool CorrectAnswer { get; set; }   
        public bool Multiple { get; set; }        
        public Guid CreatedById { get; set; }
        public Guid? ModifiedById { get; set; }
        public String CourseId { get; set; }
        public ICollection<QuestionChoice> Choices { get; set; } = new List<QuestionChoice>();
    }

    public class QuestionChoice
    {
        public Guid Id { get; set; }
        public Guid QuestionId { get; set; }      // FK -> questions.id
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }

    public class QuizResponse
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }       // FK -> users.id
        public Guid TopicId { get; set; }         // FK -> topics.id
        public string? Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public ICollection<QuizResponseAnswer> Answers { get; set; } = new List<QuizResponseAnswer>();
    }

    public class QuizResponseAnswer
    {
        public Guid Id { get; set; }
        public Guid QuizResponseId { get; set; }  // FK -> quiz_responses.id
        public string? Question { get; set; }     
        public string? Answer { get; set; }       
        public decimal? Mark { get; set; }
    }
}
