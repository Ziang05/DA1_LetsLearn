using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class TopicPage
    {
        public Guid TopicId { get; set; }     // UUID PK=FK -> topics.id 
        public string? Description { get; set; }
        public string? Content { get; set; }  // dài
    }

    public class TopicFile
    {
        public Guid TopicId { get; set; }     // PK=FK -> topics.id
        public string? Description { get; set; }
        public CloudinaryFile? File { get; set; }
    }

    public class TopicLink
    {
        public Guid TopicId { get; set; }     // PK=FK -> topics.id
        public string? Description { get; set; }
        public string? Url { get; set; }      // VARCHAR
    }

    public class TopicMeeting
    {
        public Guid TopicId { get; set; }     // PK=FK -> topics.id
        public string? Description { get; set; }
        public DateTime? Open { get; set; }
        public DateTime? Close { get; set; }
        public ICollection<TopicMeetingHistory> Histories { get; set; } = new List<TopicMeetingHistory>();
    }

    public class TopicMeetingHistory
    {
        public Guid Id { get; set; }
        public Guid TopicMeetingId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int AttendeeCount { get; set; }
        public string? AttendanceCsvUrl { get; set; }
    }

    public class TopicQuiz
    {
        public Guid TopicId { get; set; }            // UUID PK=FK -> topics.id
        public string? Description { get; set; }
        public DateTime? Open { get; set; }          // TIMESTAMP
        public DateTime? Close { get; set; }         // TIMESTAMP
        public int? TimeLimit { get; set; }
        public string? TimeLimitUnit { get; set; }
        public decimal? GradeToPass { get; set; }
        public string? GradingMethod { get; set; }
        public string? AttemptAllowed { get; set; }
        public ICollection<TopicQuizQuestion> Questions { get; set; } = new List<TopicQuizQuestion>();

    }

    public class TopicQuizQuestion
    {
        public Guid Id { get; set; }                 // UUID PK
        public Guid TopicQuizId { get; set; }        // UUID FK -> topic_quiz.id
        public string? QuestionName { get; set; }
        public string? QuestionText { get; set; }
        public string? Type { get; set; }           // True/False   Short Answer    Multiple Choice
        public decimal? DefaultMark { get; set; }
        public string? FeedbackOfTrue { get; set; }
        public string? FeedbackOfFalse { get; set; }
        public bool? CorrectAnswer { get; set; }
        public bool? Multiple { get; set; }
        public ICollection<TopicQuizQuestionChoice> Choices { get; set; } = new List<TopicQuizQuestionChoice>();

    }

    public class TopicQuizQuestionChoice
    {
        public Guid Id { get; set; }                 // UUID PK
        public Guid QuizQuestionId { get; set; }     // UUID FK -> topic_quiz_question.id
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }
    public class TopicAssignment
    {
        public Guid TopicId { get; set; }         // UUID PK=FK -> topics.id
        public string? Description { get; set; }
        public DateTime? Open { get; set; }       // TIMESTAMP
        public DateTime? Close { get; set; }      // TIMESTAMP
        public int? MaximumFile { get; set; }
        public string? MaximumFileSize { get; set; }
        public DateTime? RemindToGrade { get; set; }
        public ICollection<CloudinaryFile> Files { get; set; } = new List<CloudinaryFile>();

    }

    public class AssignmentResponse
    {
        public Guid Id { get; set; }              // UUID PK
        public Guid StudentId { get; set; }       // UUID FK -> users.id
        public Guid TopicId { get; set; }         // UUID FK -> topics.id
        public DateTime? SubmittedAt { get; set; }
        public string? Note { get; set; }
        public decimal? Mark { get; set; }
        public DateTime? GradedAt { get; set; }
        public Guid? GradedBy { get; set; }       // UUID FK -> users.id
        public ICollection<CloudinaryFile> Files { get; set; } = new List<CloudinaryFile>();

    }

    public class CloudinaryFile
    {
        public Guid Id { get; set; }              // UUID PK
        public string? Name { get; set; }
        public string? DisplayUrl { get; set; }
        public string? DownloadUrl { get; set; }
        public Guid? AssignmentResponseId { get; set; }
        public Guid? TopicFileId { get; set; }
        public Guid? TopicAssignmentId { get; set; }

    }
}
