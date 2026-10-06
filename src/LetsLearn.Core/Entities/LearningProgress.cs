namespace LetsLearn.Core.Entities
{
    public class LearningProgress
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

    public class TopicProgress
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public Guid TopicId { get; set; }
        public string Status { get; set; } = "not_started";
        public DateTime? FirstAccessedAt { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletionSource { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
