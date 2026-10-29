using System.Security.Claims;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("courses/{courseId}/recommendations")]
    public sealed class LearningRecommendationController : ControllerBase
    {
        private readonly ILearningRecommendationService _recommendationService;
        private readonly ILogger<LearningRecommendationController> _logger;

        public LearningRecommendationController(
            ILearningRecommendationService recommendationService,
            ILogger<LearningRecommendationController> logger)
        {
            _recommendationService = recommendationService;
            _logger = logger;
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(LearningRecommendationsResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<LearningRecommendationsResponseDto>> GetMyRecommendations(
            [FromRoute] string courseId,
            [FromQuery] int limit = 3,
            CancellationToken ct = default)
        {
            var userIdValue = User.Claims.FirstOrDefault(claim => claim.Type == "userID")?.Value
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var studentId) || studentId == Guid.Empty)
            {
                return Unauthorized(new { message = "Invalid or missing user identity." });
            }

            try
            {
                return Ok(await _recommendationService.GetMyRecommendationsAsync(courseId, studentId, limit, ct));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate learning recommendations for course {CourseId}.", courseId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not load learning recommendations." });
            }
        }
    }
}
