using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("course/{courseId}/[controller]")]
    [Authorize]
    public class TopicController : ControllerBase
    {
        private readonly ITopicService _topicService;
        private readonly ILogger<TopicController> _logger;

        public TopicController(ITopicService topicService, ILogger<TopicController> logger)
        {
            _topicService = topicService;
            _logger = logger;
        }

        // POST /course/{courseId}/topic
        [HttpPost]
        [ProducesResponseType(typeof(TopicResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TopicResponse>> CreateTopic(
        [FromRoute] string courseId,
        [FromBody] CreateTopicRequest request,
        CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Creating topic for section {SectionId} in course {CourseId}",
                    request.SectionId, courseId);

                var created = await _topicService.CreateTopicAsync(request, ct);
                _logger.LogInformation("Topic {TopicId} created successfully for section {SectionId}",
                    created.Id, request.SectionId);

                return CreatedAtAction(
                    nameof(GetTopicById),
                    new { courseId, id = created.Id },
                    created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating topic for section {SectionId}", request.SectionId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create topic");
            }
        }

        // PUT /course/{courseId}/topic/{id}
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(TopicResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TopicResponse>> UpdateTopic(
        [FromRoute] string courseId,
        [FromRoute] Guid id,
        [FromBody] UpdateTopicRequest request,
        CancellationToken ct)
        {
            try
            {
                if (id != request.Id)
                {
                    _logger.LogWarning("Mismatched IDs: URL ID {UrlId} != Body ID {BodyId}", id, request.Id);
                    return BadRequest("The ID in the URL must match the ID in the request body");
                }

                _logger.LogInformation("Updating topic {TopicId} in course {CourseId}", id, courseId);

                var updated = await _topicService.UpdateTopicAsync(request, ct);
                if (updated is null)
                {
                    _logger.LogWarning("Topic {TopicId} not found", id);
                    return NotFound();
                }

                _logger.LogInformation("Topic {TopicId} updated successfully", id);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating topic {TopicId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update topic");
            }
        }

        // DELETE /course/{courseId}/topic/{id}
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTopic(
        [FromRoute] string courseId,
        [FromRoute] Guid id,
        CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Deleting topic {TopicId} from course {CourseId}", id, courseId);

                var deleted = await _topicService.DeleteTopicAsync(id, ct);
                if (!deleted)
                {
                    _logger.LogWarning("Topic {TopicId} not found", id);
                    return NotFound();
                }

                _logger.LogInformation("Topic {TopicId} deleted successfully", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting topic {TopicId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete topic");
            }
        }

        // GET /course/{courseId}/topic/{id}
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(TopicResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TopicResponse>> GetTopicById(
        [FromRoute] string courseId,
        [FromRoute] Guid id,
        CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Fetching topic {TopicId} from course {CourseId}", id, courseId);

                var dto = await _topicService.GetTopicByIdAsync(id, ct);
                if (dto is null)
                {
                    _logger.LogWarning("Topic {TopicId} not found", id);
                    return NotFound();
                }

                _logger.LogInformation("Topic {TopicId} fetched successfully", id);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching topic {TopicId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to fetch topic");
            }
        }

        // GET /topic/{id}/quiz-report
        [HttpGet("{id:guid}/quiz-report")]
        [ProducesResponseType(typeof(SingleQuizReportDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SingleQuizReportDTO>> GetQuizReport(
            [FromRoute] String courseId,
            [FromRoute] Guid id,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Fetching quiz report for topic {TopicId} in course {CourseId}", id, courseId);

                var report = await _topicService.GetSingleQuizReportAsync(courseId, id, ct);
                if (report is null)
                {
                    _logger.LogWarning("Quiz report not found for topic {TopicId}", id);
                    return NotFound();
                }

                _logger.LogInformation("Quiz report for topic {TopicId} retrieved successfully", id);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching quiz report for topic {TopicId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to fetch quiz report");
            }
        }

        // GET /topic/{id}/assignment-report
        [HttpGet("{id:guid}/assignment-report")]
        [ProducesResponseType(typeof(SingleAssignmentReportDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SingleAssignmentReportDTO>> GetAssignmentReport(
            [FromRoute] String courseId,
            [FromRoute] Guid id,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Fetching assignment report for topic {TopicId} in course {CourseId}", id, courseId);

                var report = await _topicService.GetSingleAssignmentReportAsync(courseId, id, ct);
                if (report is null)
                {
                    _logger.LogWarning("Assignment report not found for topic {TopicId}", id);
                    return NotFound();
                }

                _logger.LogInformation("Assignment report for topic {TopicId} retrieved successfully", id);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching assignment report for topic {TopicId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to fetch assignment report");
            }
        }

        // POST /course/{courseId}/topic/{id}/meeting-history
        [HttpPost("{id:guid}/meeting-history")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SaveMeetingHistory(
            [FromRoute] string courseId,
            [FromRoute] Guid id,
            [FromBody] SaveMeetingHistoryRequest request,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Saving meeting history for topic {TopicId} in course {CourseId}", id, courseId);

                var result = await _topicService.SaveMeetingHistoryAsync(id, request, ct);
                if (!result)
                {
                    _logger.LogWarning("Topic meeting {TopicId} not found", id);
                    return NotFound();
                }

                return Ok(new { message = "History saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving meeting history for topic {TopicId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save meeting history");
            }
        }
    }
}
