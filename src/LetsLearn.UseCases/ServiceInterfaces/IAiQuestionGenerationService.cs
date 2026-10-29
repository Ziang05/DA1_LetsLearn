using LetsLearn.UseCases.DTOs;
using Microsoft.AspNetCore.Http;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IAiQuestionGenerationService
    {
        Task<UploadLectureDocumentResponse> UploadDocumentAsync(IFormFile file, string courseId, Guid userId, bool bypassTeacherCheck = false, CancellationToken ct = default);
        Task<GenerateAiQuestionsResponse> GenerateQuestionsAsync(GenerateAiQuestionsRequest request, Guid userId, bool bypassTeacherCheck = false, CancellationToken ct = default);
        Task ProcessGenerationJobAsync(Guid jobId, CancellationToken ct = default);
        Task<GenerateAiQuestionsResponse> GetJobAsync(Guid jobId, Guid userId, CancellationToken ct = default);
        Task<List<AiGeneratedQuestionResponse>> GetGeneratedQuestionsAsync(Guid jobId, Guid userId, CancellationToken ct = default);
        Task<ApproveGeneratedQuestionsResponse> ApproveQuestionsAsync(ApproveGeneratedQuestionsRequest request, Guid userId, CancellationToken ct = default);
    }

    public interface IDocumentTextExtractor
    {
        Task<ExtractedDocument> ExtractAsync(IFormFile file, string savedPath, CancellationToken ct = default);
        Task<ExtractedDocument> ExtractAsync(string fileName, string savedPath, CancellationToken ct = default);
    }

    public interface IAssignmentSubmissionExtractor
    {
        Task<AssignmentSubmissionSnapshot> ExtractAsync(
            LetsLearn.Core.Entities.AssignmentResponse submission,
            CancellationToken ct = default);
    }

    public interface IAssignmentGradingService
    {
        Task<LetsLearn.UseCases.DTOs.ValidatedAssignmentGrade> GradeAsync(
            LetsLearn.UseCases.DTOs.AssignmentGradingInput input,
            LetsLearn.Core.Entities.AssignmentRubric rubric,
            CancellationToken ct = default);
    }

    public interface IAiAssignmentGradingJobQueue
    {
        ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default);
        ValueTask<Guid> DequeueAsync(CancellationToken ct = default);
    }

    public interface IAiAssignmentGradingJobService
    {
        Task<LetsLearn.UseCases.DTOs.AiAssignmentGradingJobRequestResult> RequestAsync(
            Guid assignmentResponseId,
            Guid requesterId,
            CancellationToken ct = default);
    }

    public interface IAiAssignmentGradingJobProcessor
    {
        Task ProcessAsync(Guid jobId, CancellationToken ct = default);
    }

    public interface IAiAssignmentGradingReviewService
    {
        Task<LetsLearn.UseCases.DTOs.AssignmentRubricResponse> UpsertRubricAsync(Guid topicId, LetsLearn.UseCases.DTOs.UpsertAssignmentRubricRequest request, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<LetsLearn.UseCases.DTOs.AssignmentRubricResponse> GetRubricAsync(Guid topicId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<LetsLearn.UseCases.DTOs.AiAssignmentGradingJobRequestResult> RequestJobAsync(Guid responseId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<LetsLearn.UseCases.DTOs.AiAssignmentGradingJobResponse> GetJobAsync(Guid jobId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<LetsLearn.UseCases.DTOs.AssignmentGradingHistoryResponse> GetHistoryAsync(Guid responseId, int page, int pageSize, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<LetsLearn.UseCases.DTOs.PublishedAssignmentGradeResponse> PublishDraftAsync(Guid draftId, LetsLearn.UseCases.DTOs.PublishAiAssignmentGradeRequest request, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task RejectDraftAsync(Guid draftId, LetsLearn.UseCases.DTOs.RejectAiAssignmentGradeRequest request, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<LetsLearn.UseCases.DTOs.PublishedAssignmentGradeResponse> GetPublishedGradeAsync(Guid responseId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
    }

    public interface IDocumentChunkingService
    {
        List<LectureChunkDto> Chunk(ExtractedDocument document);
    }

    public interface IAiLlmClient
    {
        Task<string?> GenerateJsonAsync(string systemPrompt, string userPrompt, decimal temperature, CancellationToken ct = default, string modelConfigKey = "GeneratorModel");
        Task<string?> GenerateJsonWithOptionsAsync(
            string systemPrompt,
            string userPrompt,
            decimal temperature,
            CancellationToken ct,
            string modelConfigKey,
            TimeSpan requestTimeout,
            string modelOverride);
    }

    public interface IAiEmbeddingClient
    {
        Task<List<float[]?>> CreateEmbeddingsAsync(IEnumerable<string> inputs, CancellationToken ct = default);
    }

    public interface IAiVectorStore
    {
        Task UpsertChunkEmbeddingsAsync(IEnumerable<LectureChunkVector> chunks, CancellationToken ct = default);
        Task<List<Guid>> SearchSimilarChunksAsync(Guid documentId, float[] queryEmbedding, int topK, CancellationToken ct = default);
    }

    public interface IAiQuotaService
    {
        Task CheckAndConsumeAsync(Guid userId, string metric, int amount, int limit, TimeSpan window, CancellationToken ct = default);
    }

    public interface IAiQuestionGenerationJobQueue
    {
        ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default);
        ValueTask<Guid> DequeueAsync(CancellationToken ct = default);
    }

    public class AiQuotaExceededException : Exception
    {
        public AiQuotaExceededException(string message) : base(message)
        {
        }
    }

    public class LectureChunkVector
    {
        public Guid ChunkId { get; set; }
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
