namespace LetsLearn.Core.Entities
{
    public class LectureDocument
    {
        public Guid Id { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public Guid UploadedById { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Status { get; set; } = "processed";
        public DateTime CreatedAt { get; set; }
        public ICollection<LectureChunk> Chunks { get; set; } = new List<LectureChunk>();
    }

    public class LectureChunk
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public string? Heading { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? EmbeddingJson { get; set; }
        public string? EmbeddingModel { get; set; }
        public int ChunkOrder { get; set; }
        public int? PageNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AiQuestionGenerationJob
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public Guid CreatedById { get; set; }
        public string BloomLevel { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "MultipleChoice";
        public int QuestionCount { get; set; }
        public int RetrievalTopK { get; set; } = 5;
        public string? KnowledgePoint { get; set; }
        public string Status { get; set; } = "pending";
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public ICollection<AiGeneratedQuestion> Questions { get; set; } = new List<AiGeneratedQuestion>();
    }

    public class AiGeneratedQuestion
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public string QuestionName { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string Type { get; set; } = "MultipleChoice";
        public string BloomLevel { get; set; } = string.Empty;
        public string Status { get; set; } = "draft";
        public decimal Score { get; set; }
        public string? GroundingRefsJson { get; set; }
        public string? ChoicesJson { get; set; }
        public string? Feedback { get; set; }
        public int Attempt { get; set; }
        public Guid CreatedById { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AiEvaluationResult
    {
        public Guid Id { get; set; }
        public Guid GeneratedQuestionId { get; set; }
        public decimal BloomAlignment { get; set; }
        public decimal Grounding { get; set; }
        public decimal Clarity { get; set; }
        public decimal DistractorQuality { get; set; }
        public decimal FinalScore { get; set; }
        public string? ReasonsJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
