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
    public class LearningProgressController : ControllerBase
    {
        private readonly ILearningProgressService _learningProgressService;
        private readonly ILogger<LearningProgressController> _logger;

        public LearningProgressController(
            ILearningProgressService learningProgressService,
            ILogger<LearningProgressController> logger)
        {
            _learningProgressService = learningProgressService;
            _logger = logger;
        }

        [HttpGet("courses/{courseId}/progress/me")]
        [ProducesResponseType(typeof(LearningProgressSummaryDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<LearningProgressSummaryDto>> GetMyCourseProgress(
            [FromRoute] string courseId,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _learningProgressService.GetMyCourseProgressAsync(courseId, GetUserId(), ct));
            }
            catch (Exception ex)
            {
                return HandleProgressException(ex, "Failed to get course progress. CourseId={CourseId}", courseId);
            }
        }

        [HttpGet("courses/{courseId}/progress/me/topics")]
        [ProducesResponseType(typeof(List<TopicProgressDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<TopicProgressDto>>> GetMyTopicProgresses(
            [FromRoute] string courseId,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _learningProgressService.GetMyTopicProgressesAsync(courseId, GetUserId(), ct));
            }
            catch (Exception ex)
            {
                return HandleProgressException(ex, "Failed to get topic progresses. CourseId={CourseId}", courseId);
            }
        }

        [HttpPost("topics/{topicId:guid}/progress/view")]
        [ProducesResponseType(typeof(TopicProgressDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<TopicProgressDto>> MarkTopicViewed(
            [FromRoute] Guid topicId,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _learningProgressService.MarkTopicViewedAsync(topicId, GetUserId(), ct));
            }
            catch (Exception ex)
            {
                return HandleProgressException(ex, "Failed to mark topic viewed. TopicId={TopicId}", topicId);
            }
        }

        [HttpPost("topics/{topicId:guid}/progress/complete")]
        [ProducesResponseType(typeof(TopicProgressDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<TopicProgressDto>> MarkTopicCompleted(
            [FromRoute] Guid topicId,
            [FromBody] CompleteTopicProgressRequest? request,
            CancellationToken ct)
        {
            try
            {
                var completionSource = request?.CompletionSource ?? "manual";
                return Ok(await _learningProgressService.MarkTopicCompletedAsync(topicId, GetUserId(), completionSource, ct));
            }
            catch (Exception ex)
            {
                return HandleProgressException(ex, "Failed to mark topic completed. TopicId={TopicId}", topicId);
            }
        }

        [HttpGet("courses/{courseId}/progress/students")]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Teacher}")]
        [ProducesResponseType(typeof(List<CourseStudentProgressDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CourseStudentProgressDto>>> GetCourseStudentProgresses(
            [FromRoute] string courseId,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _learningProgressService.GetCourseStudentProgressesAsync(courseId, GetUserId(), GetRole(), ct));
            }
            catch (Exception ex)
            {
                return HandleProgressException(ex, "Failed to get course student progresses. CourseId={CourseId}", courseId);
            }
        }

        [HttpGet("courses/{courseId}/progress/students/{studentId:guid}/topics")]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Teacher}")]
        [ProducesResponseType(typeof(List<TopicProgressDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<TopicProgressDto>>> GetStudentTopicProgressesForCourse(
            [FromRoute] string courseId,
            [FromRoute] Guid studentId,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _learningProgressService.GetStudentTopicProgressesForCourseAsync(
                    courseId,
                    studentId,
                    GetUserId(),
                    GetRole(),
                    ct));
            }
            catch (Exception ex)
            {
                return HandleProgressException(ex, "Failed to get student topic progresses. CourseId={CourseId}, StudentId={StudentId}", courseId, studentId);
            }
        }

        [HttpPost("courses/{courseId}/progress/sync")]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Teacher}")]
        [ProducesResponseType(typeof(LearningProgressSyncResultDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<LearningProgressSyncResultDto>> SyncCourseProgressFromResponses(
            [FromRoute] string courseId,
            CancellationToken ct)
        {
            try
            {
                return Ok(await _learningProgressService.SyncCourseProgressFromResponsesAsync(courseId, GetUserId(), GetRole(), ct));
            }
            catch (Exception ex)
            {
                return HandleProgressException(ex, "Failed to sync course progress. CourseId={CourseId}", courseId);
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

        private string? GetRole()
        {
            return User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        }

        private ActionResult HandleProgressException(Exception ex, string messageTemplate, params object[] args)
        {
            switch (ex)
            {
                case UnauthorizedAccessException:
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
                case KeyNotFoundException:
                    return NotFound(new { message = ex.Message });
                case ArgumentException:
                    return BadRequest(new { message = ex.Message });
                default:
                    _logger.LogError(ex, messageTemplate, args);
                    return BadRequest(new { message = ex.Message });
            }
        }
    }
}
