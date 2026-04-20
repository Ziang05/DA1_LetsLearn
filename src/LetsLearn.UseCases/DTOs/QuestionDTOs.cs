using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class CreateQuestionChoiceRequest
    {
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }

    public class UpdateQuestionChoiceRequest
    {
        public Guid? Id { get; set; }
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }

    public class GetQuestionChoiceResponse
    {
        public Guid Id { get; set; }
        public Guid QuestionId { get; set; }
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }
    public class CreateQuestionCourse
    {
        public string? Id { get; set; }
    }
    public class CreateQuestionRequest
    {
        public String? CourseId { get; set; }
        public CreateQuestionCourse? Course { get; set; }
        public string? QuestionName { get; set; }
        public string? QuestionText { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
        public double? DefaultMark { get; set; }
        public long? Usage { get; set; }
        public string? FeedbackOfTrue { get; set; }
        public string? FeedbackOfFalse { get; set; }
        public bool? CorrectAnswer { get; set; }     
        public bool Multiple { get; set; }        

        public List<CreateQuestionChoiceRequest>? Choices { get; set; }
    }

    public class UpdateQuestionRequest
    {
        public Guid Id { get; set; }
        public String? CourseId { get; set; }
        public string? QuestionName { get; set; }
        public string? QuestionText { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
        public double? DefaultMark { get; set; }
        public long? Usage { get; set; }
        public string? FeedbackOfTrue { get; set; }
        public string? FeedbackOfFalse { get; set; }
        public bool? CorrectAnswer { get; set; }
        public bool Multiple { get; set; }

        public List<UpdateQuestionChoiceRequest>? Choices { get; set; }
    }

    public class GetQuestionResponse
    {
        public Guid Id { get; set; }

        public Guid CreatedById { get; set; }
        public Guid? ModifiedById { get; set; }
        public String CourseId { get; set; }
        public string? CourseName { get; set; }

        public string? QuestionName { get; set; }
        public string? QuestionText { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
        public double? DefaultMark { get; set; }
        public long? Usage { get; set; }
        public string? FeedbackOfTrue { get; set; }
        public string? FeedbackOfFalse { get; set; }
        public bool? CorrectAnswer { get; set; }     
        public bool Multiple { get; set; }          

        public List<GetQuestionChoiceResponse>? Choices { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
