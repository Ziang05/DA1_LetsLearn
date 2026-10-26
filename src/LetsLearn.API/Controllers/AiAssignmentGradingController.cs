using System.Security.Claims;
using LetsLearn.Core.Shared;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Authorize]
    public sealed class AiAssignmentGradingController : ControllerBase
    {
        private readonly IAiAssignmentGradingReviewService _gradingService;

        public AiAssignmentGradingController(IAiAssignmentGradingReviewService gradingService)
        {
            _gradingService = gradingService;
        }

        [HttpPut("/topics/{topicId:guid}/grading-rubric")]
        public async Task<ActionResult<AssignmentRubricResponse>> UpsertRubric(
            Guid topicId,
            [FromBody] UpsertAssignmentRubricRequest request,
            CancellationToken ct)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                return Ok(await _gradingService.UpsertRubricAsync(topicId, request, callerId, role, ct));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("/topics/{topicId:guid}/grading-rubric")]
        public async Task<ActionResult<AssignmentRubricResponse>> GetRubric(Guid topicId, CancellationToken ct)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                return Ok(await _gradingService.GetRubricAsync(topicId, callerId, role, ct));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("/assignment-responses/{responseId:guid}/ai-grading/jobs")]
        public async Task<ActionResult<AiAssignmentGradingJobAcceptedResponse>> RequestJob(Guid responseId, CancellationToken ct)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                var result = await _gradingService.RequestJobAsync(responseId, callerId, role, ct);
                var statusUrl = Url.Link("GetAiAssignmentGradingJob", new { jobId = result.JobId })
                    ?? $"/ai-grading/jobs/{result.JobId:D}";
                var accepted = new AiAssignmentGradingJobAcceptedResponse
                {
                    JobId = result.JobId,
                    Status = result.Status,
                    StatusUrl = statusUrl,
                    CreatedAt = result.CreatedAt
                };
                return Accepted(statusUrl, accepted);
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("/ai-grading/jobs/{jobId:guid}", Name = "GetAiAssignmentGradingJob")]
        public async Task<ActionResult<AiAssignmentGradingJobResponse>> GetJob(Guid jobId, CancellationToken ct)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                return Ok(await _gradingService.GetJobAsync(jobId, callerId, role, ct));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("/assignment-responses/{responseId:guid}/ai-grading/history")]
        public async Task<ActionResult<AssignmentGradingHistoryResponse>> GetHistory(
            Guid responseId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                return Ok(await _gradingService.GetHistoryAsync(responseId, page, pageSize, callerId, role, ct));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("/ai-grading/drafts/{draftId:guid}/publish")]
        public async Task<ActionResult<PublishedAssignmentGradeResponse>> PublishDraft(
            Guid draftId,
            [FromBody] PublishAiAssignmentGradeRequest request,
            CancellationToken ct)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                return Ok(await _gradingService.PublishDraftAsync(draftId, request, callerId, role, ct));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("/ai-grading/drafts/{draftId:guid}/reject")]
        public async Task<IActionResult> RejectDraft(
            Guid draftId,
            [FromBody] RejectAiAssignmentGradeRequest request,
            CancellationToken ct)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                await _gradingService.RejectDraftAsync(draftId, request, callerId, role, ct);
                return NoContent();
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("/assignment-responses/{responseId:guid}/grade")]
        public async Task<ActionResult<PublishedAssignmentGradeResponse>> GetPublishedGrade(Guid responseId, CancellationToken ct)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                return Ok(await _gradingService.GetPublishedGradeAsync(responseId, callerId, role, ct));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        private bool TryGetCaller(out Guid callerId, out string? role)
        {
            var id = User.FindFirstValue("userID") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            role = User.FindFirstValue(ClaimTypes.Role);
            return Guid.TryParse(id, out callerId) && callerId != Guid.Empty;
        }

        private ActionResult HandleException(Exception exception)
        {
            if (exception is UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new AiAssignmentGradingApiError { Code = "forbidden", Message = exception.Message });
            }
            if (exception is KeyNotFoundException)
            {
                return NotFound(new AiAssignmentGradingApiError { Code = "not_found", Message = exception.Message });
            }
            if (exception is AiQuotaExceededException)
            {
                Response.Headers.RetryAfter = "60";
                return StatusCode(StatusCodes.Status429TooManyRequests, new AiAssignmentGradingApiError { Code = "quota_exceeded", Message = "Đã vượt hạn mức yêu cầu chấm bài." });
            }
            if (exception is AssignmentGradingException gradingException)
            {
                var status = gradingException.Code.Contains("conflict", StringComparison.Ordinal)
                    || gradingException.Code.Contains("stale", StringComparison.Ordinal)
                    || gradingException.Code is "draft_not_reviewable" or "grade_changed"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status422UnprocessableEntity;
                return StatusCode(status, new AiAssignmentGradingApiError { Code = gradingException.Code, Message = gradingException.Message });
            }
            if (exception is ArgumentException)
            {
                return BadRequest(new AiAssignmentGradingApiError { Code = "invalid_request", Message = exception.Message });
            }
            if (exception is InvalidOperationException)
            {
                return Conflict(new AiAssignmentGradingApiError { Code = "conflict", Message = exception.Message });
            }

            return StatusCode(StatusCodes.Status500InternalServerError,
                new AiAssignmentGradingApiError { Code = "ai_grading_error", Message = "AI grading operation failed." });
        }
    }
}
