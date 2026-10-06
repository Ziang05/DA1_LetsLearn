namespace LetsLearn.Core.Entities
{
    public class LearningActivityLog
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? CourseId { get; set; }
        public Guid? TopicId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? EventSource { get; set; }
        public string? Metadata { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
