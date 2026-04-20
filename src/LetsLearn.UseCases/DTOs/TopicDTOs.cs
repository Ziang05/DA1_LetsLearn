using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class TopicDTO
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public Guid SectionId { get; set; }

        [Required(ErrorMessage = "Title cannot be empty")]
        public string Title { get; set; } = null!;

        public string? Type { get; set; }

        public Object? Data { get; set; }

        public int? StudentCount { get; set; }

        public Object? Response { get; set; }

        public GetCourseResponse? Course { get; set; }
    }

    public class TopicResponse
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Type { get; set; }   // quiz / assignment / meeting
        public object? Data { get; set; }
        public Guid SectionId { get; set; }
    }

    public class TopicRequest
    {
        public Guid? Id { get; set; }
        public string? Title { get; set; }
        public string? Type { get; set; }   // quiz / assignment / meeting
        public Guid SectionId { get; set; }

    }

    public class CreateTopicRequest
    {
        public string? Title { get; set; }
        public string Type { get; set; } = default!;   // "page", "file", "link", "quiz", "assignment", "meeting"
        public Guid SectionId { get; set; }
        public string? Data { get; set; }              // phần dữ liệu riêng cho từng type
    }

    public class CreateTopicAssignmentRequest
    {
        public string? Description { get; set; }
        public DateTime? Open { get; set; }       // TIMESTAMP
        public DateTime? Close { get; set; }      // TIMESTAMP
        public int? MaximumFile { get; set; }
        public string? MaximumFileSize { get; set; }
        public DateTime? RemindToGrade { get; set; }

        // Add files collection to match frontend data
        public List<TopicFileData> CloudinaryFiles { get; set; } = new List<TopicFileData>();
    }

    public class CreateTopicQuizRequest
    {
        public string? Description { get; set; }
        public DateTime? Open { get; set; }
        public DateTime? Close { get; set; }
        public int? TimeLimit { get; set; }
        public string? TimeLimitUnit { get; set; }
        public decimal? GradeToPass { get; set; }
        public string? GradingMethod { get; set; }  // Highest Grade    Average Grade   First Grade     Last Grade
        public string? AttemptAllowed { get; set; }

        public ICollection<TopicQuizQuestionRequest> Questions { get; set; } = new List<TopicQuizQuestionRequest>();
    }

    public class TopicQuizQuestionRequest
    {
        public string? QuestionName { get; set; }
        public string? QuestionText { get; set; }
        public string? Type { get; set; }   // Multiple Choice      True/False      Short Answer
        public decimal? DefaultMark { get; set; }
        public string? FeedbackOfTrue { get; set; }
        public string? FeedbackOfFalse { get; set; }
        public bool? CorrectAnswer { get; set; }
        public bool? Multiple { get; set; }
        public ICollection<TopicQuizQuestionChoiceRequest> Choices { get; set; } = new List<TopicQuizQuestionChoiceRequest>();
    }

    public class TopicQuizQuestionChoiceRequest
    {
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }

    public class CreateTopicPageRequest
    {
        public string? Description { get; set; }
        public string? Content { get; set; }
    }

    // Updated to handle file data structure from frontend
    public class CreateTopicFileRequest
    {
        public string? Description { get; set; }
        public TopicFileData? File { get; set; }  // Changed from FileUrl to File object
    }

    public class CreateTopicLinkRequest
    {
        public string? Description { get; set; }
        public string? Url { get; set; }
    }

    public class CreateTopicMeetingRequest
    {
        public string? Description { get; set; }
        public DateTime? Open { get; set; }
        public DateTime? Close { get; set; }
    }

    public class UpdateTopicRequest
    {
        public Guid? Id { get; set; }
        public string? Title { get; set; }
        public string Type { get; set; } = default!;   // "page", "file", "link", "quiz", "assignment", "meeting"
        public string? Data { get; set; }
    }

    // Quiz
    public class UpdateTopicQuizRequest : UpdateTopicRequest
    {
        public string? Description { get; set; }
        public DateTime? Open { get; set; }
        public DateTime? Close { get; set; }
        public int? TimeLimit { get; set; }
        public string? TimeLimitUnit { get; set; }
        public decimal? GradeToPass { get; set; }
        public string? GradingMethod { get; set; }
        public string? AttemptAllowed { get; set; }
        public ICollection<UpdateTopicQuizQuestionRequest> Questions { get; set; } = new List<UpdateTopicQuizQuestionRequest>();
    }

    public class UpdateTopicQuizQuestionRequest
    {
        public Guid? Id { get; set; }
        public string? QuestionName { get; set; }
        public string? QuestionText { get; set; }
        public string? Type { get; set; }
        public decimal? DefaultMark { get; set; }
        public string? FeedbackOfTrue { get; set; }
        public string? FeedbackOfFalse { get; set; }
        public bool? CorrectAnswer { get; set; }
        public bool? Multiple { get; set; }
        public ICollection<UpdateTopicQuizQuestionChoiceRequest> Choices { get; set; } = new List<UpdateTopicQuizQuestionChoiceRequest>();
    }

    public class UpdateTopicQuizQuestionChoiceRequest
    {
        public Guid? Id { get; set; }
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }

    // Assignment
    public class UpdateTopicAssignmentRequest : UpdateTopicRequest
    {
        public string? Description { get; set; }
        public DateTime? Open { get; set; }
        public DateTime? Close { get; set; }
        public int? MaximumFile { get; set; }
        public string? MaximumFileSize { get; set; }
        public DateTime? RemindToGrade { get; set; }

        // Add files collection to match frontend data
        public List<TopicFileData> CloudinaryFiles { get; set; } = new List<TopicFileData>();
    }

    // File - INHERITANCE PRESERVED, just added File property
    public class UpdateTopicFileRequest : UpdateTopicRequest
    {
        public string? Description { get; set; }
        public TopicFileData? File { get; set; }  // Added to handle file data from frontend
    }

    // Link
    public class UpdateTopicLinkRequest : UpdateTopicRequest
    {
        public string? Description { get; set; }
        public string? Url { get; set; }
    }

    // Page
    public class UpdateTopicPageRequest : UpdateTopicRequest
    {
        public string? Description { get; set; }
        public string? Content { get; set; }
    }

    // Meeting
    public class UpdateTopicMeetingRequest : UpdateTopicRequest
    {
        public string? Description { get; set; }
        public DateTime? Open { get; set; }
        public DateTime? Close { get; set; }
    }

    public class SaveMeetingHistoryRequest
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int AttendeeCount { get; set; }
        public string? AttendanceCsvUrl { get; set; }
    }

    // New helper class for file data structure
    public class TopicFileData
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? DisplayUrl { get; set; }
        public string? DownloadUrl { get; set; }
    }

    public class TopicUpsertDTO
    {
        public Guid? Id { get; set; }
        public string? Title { get; set; }
        public string? Type { get; set; }
    }

    public class SingleAssignmentReportDTO
    {
        public class StudentInfoAndMarkAssignment
        {
            public GetUserResponse Student { get; set; } = default!;
            public bool Submitted { get; set; } = true; // true nếu đã nộp; false = chưa nộp
            public double? Mark { get; set; }
            public Guid? ResponseId { get; set; }
        }

        public string Name { get; set; } = string.Empty;

        public List<StudentInfoAndMarkAssignment> StudentMarks { get; set; } = new();
        public List<StudentInfoAndMarkAssignment> StudentWithMarkOver8 { get; set; } = new();
        public List<StudentInfoAndMarkAssignment> StudentWithMarkOver5 { get; set; } = new();
        public List<StudentInfoAndMarkAssignment> StudentWithMarkOver2 { get; set; } = new();
        public List<StudentInfoAndMarkAssignment> StudentWithMarkOver0 { get; set; } = new();
        public List<StudentInfoAndMarkAssignment> StudentWithNoResponse { get; set; } = new();

        public Dictionary<int, int> MarkDistributionCount { get; set; } = new()
        {
            { -1, 0 }, { 0, 0 }, { 2, 0 }, { 5, 0 }, { 8, 0 }
        };

        public int SubmissionCount { get; set; }
        public int GradedSubmissionCount { get; set; }
        public int FileCount { get; set; }

        public double AvgMark { get; set; }
        public double MaxMark { get; set; }
        public double MinMark { get; set; }
        public double CompletionRate { get; set; }

        // kể cả SV không tham gia
        public List<GetUserResponse> Students { get; set; } = new();
        public Dictionary<string, long> FileTypeCount { get; set; } = new();

        public SingleAssignmentReportDTO() { }
        public SingleAssignmentReportDTO(string name) : this()
        {
            Name = name;
        }
    }

    public class SingleQuizReportDTO
    {
        public class StudentInfoAndMarkQuiz
        {
            public GetUserResponse Student { get; set; } = default!;
            public bool Submitted { get; set; } = true;
            public double? Mark { get; set; }
            public Guid? ResponseId { get; set; }
        }

        public string Name { get; set; } = string.Empty;

        public List<StudentInfoAndMarkQuiz> StudentWithMark { get; set; } = new();

        public List<StudentInfoAndMarkQuiz> StudentWithMarkOver8 { get; set; } = new();
        public List<StudentInfoAndMarkQuiz> StudentWithMarkOver5 { get; set; } = new();
        public List<StudentInfoAndMarkQuiz> StudentWithMarkOver2 { get; set; } = new();
        public List<StudentInfoAndMarkQuiz> StudentWithMarkOver0 { get; set; } = new();
        public List<StudentInfoAndMarkQuiz> StudentWithNoResponse { get; set; } = new();

        public double MaxDefaultMark { get; set; }
        public Dictionary<int, int> MarkDistributionCount { get; set; } = new();

        public int QuestionCount { get; set; }
        public double AvgStudentMarkBase10 { get; set; }
        public double MaxStudentMarkBase10 { get; set; }
        public double MinStudentMarkBase10 { get; set; }

        public int AttemptCount { get; set; }
        public double AvgTimeSpend { get; set; } // seconds

        public double CompletionRate { get; set; }

        // count even students that don't take part in the quiz
        public List<GetUserResponse> Students { get; set; } = new();

        public int TrueFalseQuestionCount { get; set; }
        public int MultipleChoiceQuestionCount { get; set; }
        public int ShortAnswerQuestionCount { get; set; }
    }

    public class TopicDataDTO
    {
        public object? Item { get; set; }       // quiz or assignment
        public object? Response { get; set; }   // response
    }
}