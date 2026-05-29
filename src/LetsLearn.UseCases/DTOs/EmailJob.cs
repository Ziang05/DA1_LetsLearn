using System;

namespace LetsLearn.UseCases.DTOs
{
    public enum EmailType
    {
        DeadlineReminder,
        NewTopicNotification,
        PasswordReset,
        Custom
    }

    public class EmailJob
    {
        public EmailType Type { get; set; }
        public string ToEmail { get; set; } = string.Empty;
        public string? StudentName { get; set; }
        public string? CourseTitle { get; set; }
        public string? TopicTitle { get; set; }
        public string? TopicType { get; set; }
        public DateTime? Deadline { get; set; }
        public string? MeetingLink { get; set; }

        // For new topic notifications
        public DateTime? OpenDate { get; set; }
        public DateTime? CloseDate { get; set; }

        // For password reset
        public string? Username { get; set; }
        public string? NewPassword { get; set; }

        // For custom / general emails
        public string? Subject { get; set; }
        public string? Body { get; set; }
    }
}
