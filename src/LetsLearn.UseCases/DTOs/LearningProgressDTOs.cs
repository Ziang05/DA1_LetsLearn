namespace LetsLearn.UseCases.DTOs
{
    public class LearningProgressSummaryDto
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public int CompletedTopicCount { get; set; }
        public int TotalTopicCount { get; set; }
        public decimal ProgressPercent { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CourseStudentProgressDto : LearningProgressSummaryDto
    {
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentAvatar { get; set; }
        public DateTime? JoinedAt { get; set; }
    }

    public class LearningProgressSyncResultDto
    {
        public string CourseId { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int CreatedTopicProgressCount { get; set; }
        public int UpdatedTopicProgressCount { get; set; }
        public int RecalculatedProgressCount { get; set; }
        public DateTime SyncedAt { get; set; }
    }

    public class TopicProgressDto
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public Guid TopicId { get; set; }
        public string? TopicTitle { get; set; }
        public string? TopicType { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? FirstAccessedAt { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletionSource { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CompleteTopicProgressRequest
    {
        public string? CompletionSource { get; set; } = "manual";
    }
}
