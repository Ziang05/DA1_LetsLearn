using Microsoft.AspNetCore.Http;

namespace LetsLearn.UseCases.DTOs
{
    public class UploadLectureDocumentResponse
    {
        public Guid DocumentId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int ChunkCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class GenerateAiQuestionsRequest
    {
        public Guid DocumentId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public string BloomLevel { get; set; } = "Understand";
        public int QuestionCount { get; set; } = 5;
        public string QuestionType { get; set; } = "MultipleChoice";
        public int TopK { get; set; } = 5;
        public string? KnowledgePoint { get; set; }
    }

    public class GenerateAiQuestionsResponse
    {
        public Guid JobId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public string? KnowledgePoint { get; set; }
        public List<AiGeneratedQuestionResponse> Questions { get; set; } = new();
    }

    public class AiGeneratedQuestionResponse
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public string CourseId { get; set; } = string.Empty;
        public string QuestionName { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string BloomLevel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public int Attempt { get; set; }
        public List<string> GroundingRefs { get; set; } = new();
        public List<AiGeneratedChoiceDto> Choices { get; set; } = new();
        public string? Feedback { get; set; }
    }

    public class AiGeneratedChoiceDto
    {
        public string? Text { get; set; }
        public decimal? GradePercent { get; set; }
        public string? Feedback { get; set; }
    }

    public class ApproveGeneratedQuestionsRequest
    {
        public List<Guid> GeneratedQuestionIds { get; set; } = new();
    }

    public class ApproveGeneratedQuestionsResponse
    {
        public int SavedCount { get; set; }
    }

    public class LectureChunkDto
    {
        public Guid Id { get; set; }
        public string? Heading { get; set; }
        public string Content { get; set; } = string.Empty;
        public int ChunkOrder { get; set; }
        public int? PageNumber { get; set; }
    }

    public class ExtractedDocument
    {
        public string Text { get; set; } = string.Empty;
        public List<ExtractedDocumentBlock> Blocks { get; set; } = new();
    }

    public class ExtractedDocumentBlock
    {
        public string? Heading { get; set; }
        public string Text { get; set; } = string.Empty;
        public int? PageNumber { get; set; }
    }
}
