using System.Security.Claims;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("ai/questions")]
    [Authorize]
    public class AiQuestionController : ControllerBase
    {
        private readonly IAiQuestionGenerationService _service;
        private readonly ILogger<AiQuestionController> _logger;

        public AiQuestionController(IAiQuestionGenerationService service, ILogger<AiQuestionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost("documents")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<UploadLectureDocumentResponse>> UploadDocument(
            [FromForm] UploadLectureDocumentRequest request,
            [FromQuery] string? courseId,
            CancellationToken ct)
        {
            try
            {
                var resolvedCourseId = await ResolveCourseIdAsync(courseId, ct);
                var result = await _service.UploadDocumentAsync(request.File, resolvedCourseId, GetUserId(), ct: ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (AiQuotaExceededException ex)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI document upload failed.");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("generate")]
        public async Task<ActionResult<GenerateAiQuestionsResponse>> Generate(
            [FromBody] GenerateAiQuestionsRequest request,
            CancellationToken ct)
        {
            try
            {
                var result = await _service.GenerateQuestionsAsync(request, GetUserId(), ct: ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (AiQuotaExceededException ex)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI question generation failed.");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("jobs/{jobId:guid}")]
        public async Task<ActionResult<GenerateAiQuestionsResponse>> GetJob(Guid jobId, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.GetJobAsync(jobId, GetUserId(), ct));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("jobs/{jobId:guid}/questions")]
        public async Task<ActionResult<List<AiGeneratedQuestionResponse>>> GetQuestions(Guid jobId, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.GetGeneratedQuestionsAsync(jobId, GetUserId(), ct));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("approve")]
        public async Task<ActionResult<ApproveGeneratedQuestionsResponse>> Approve(
            [FromBody] ApproveGeneratedQuestionsRequest request,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _service.ApproveQuestionsAsync(request, GetUserId(), ct));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private Guid GetUserId()
        {
            var id = User.Claims.FirstOrDefault(c => c.Type == "userID")?.Value
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(id, out var userId) || userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Invalid or missing user identity.");
            }

            return userId;
        }

        private async Task<string> ResolveCourseIdAsync(string? courseId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(courseId))
            {
                return courseId;
            }

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(ct);
                if (form.TryGetValue("courseId", out var formCourseId)
                    && !string.IsNullOrWhiteSpace(formCourseId))
                {
                    return formCourseId.ToString();
                }
            }

            return string.Empty;
        }
    }
}
