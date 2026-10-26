using System.Security.Claims;
using LetsLearn.Core.Shared;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("topic/{topicId:guid}/assignment-response")]
    [Authorize]
    public class AssignmentResponseController : ControllerBase
    {
        private readonly IAssignmentResponseService _assignmentResponseService;

        public AssignmentResponseController(IAssignmentResponseService assignmentResponseService)
        {
            _assignmentResponseService = assignmentResponseService;
        }

        [HttpPost]
        public async Task<ActionResult<AssignmentResponseDTO>> CreateAssignmentResponse(
            [FromRoute] Guid topicId,
            [FromBody] CreateAssignmentResponseRequest dto,
            CancellationToken ct = default)
        {
            if (!TryGetCaller(out var userId, out var role)) return Unauthorized();
            if (!IsRole(role, AppRoles.Student))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "student_role_required",
                    message = $"Chỉ tài khoản học viên mới được nộp bài. Role trong access token hiện tại: '{role ?? "(missing)"}'. Hệ thống chấp nhận '{AppRoles.Student}' hoặc '{AppRoles.Learner}'."
                });
            }

            try
            {
                await _assignmentResponseService.ValidateStudentCanSubmitAsync(topicId, userId, ct);
                dto.TopicId = topicId;
                dto.SubmittedAt = DateTime.UtcNow;
                dto.Mark = null;
                return Ok(await _assignmentResponseService.CreateAssigmentResponseAsync(dto, userId, ct));
            }
            catch (Exception ex)
            {
                return HandleAssignmentException(ex);
            }
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<IEnumerable<AssignmentResponseDTO>>> GetAllAssignmentResponsesByTopicId(
            [FromRoute] Guid topicId,
            [FromQuery] Guid? studentId = null,
            CancellationToken ct = default)
        {
            return await GetAssignmentResponses(topicId, studentId, ct);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AssignmentResponseDTO>>> GetAssignmentResponsesByTopic(
            [FromRoute] Guid topicId,
            [FromQuery] Guid? studentId = null,
            CancellationToken ct = default)
        {
            return await GetAssignmentResponses(topicId, studentId, ct);
        }

        private async Task<ActionResult<IEnumerable<AssignmentResponseDTO>>> GetAssignmentResponses(
            Guid topicId,
            Guid? studentId,
            CancellationToken ct)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();

            try
            {
                if (IsRole(role, AppRoles.Student))
                {
                    // A student can never use the query string to inspect another student's submission.
                    return Ok(await _assignmentResponseService.GetAssigmentResponsesByTopicIdAndStudentIdAsync(
                        topicId, callerId, callerId, role, ct));
                }

                if (!IsRole(role, AppRoles.Admin) && !IsRole(role, AppRoles.Teacher)) return Forbid();

                if (studentId.HasValue)
                {
                    return Ok(await _assignmentResponseService.GetAssigmentResponsesByTopicIdAndStudentIdAsync(
                        topicId, studentId.Value, callerId, role, ct));
                }

                return Ok(await _assignmentResponseService.GetAllAssigmentResponseByTopicIdAsync(
                    topicId, callerId, role, ct));
            }
            catch (Exception ex)
            {
                return HandleAssignmentException(ex);
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AssignmentResponseDTO>> GetAssignmentResponseById(
            [FromRoute] Guid topicId,
            [FromRoute] Guid id,
            CancellationToken ct = default)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            try
            {
                return Ok(await _assignmentResponseService.GetAssigmentResponseByIdAsync(
                    id, callerId, role, topicId, ct));
            }
            catch (Exception ex)
            {
                return HandleAssignmentException(ex);
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AssignmentResponseDTO>> UpdateAssignmentResponseById(
            [FromRoute] Guid topicId,
            [FromRoute] Guid id,
            [FromBody] UpdateAssignmentResponseRequest dto,
            CancellationToken ct = default)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            if (!IsRole(role, AppRoles.Student))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "student_role_required",
                    message = $"Chỉ tài khoản học viên mới được cập nhật bài nộp. Role trong access token hiện tại: '{role ?? "(missing)"}'. Hệ thống chấp nhận '{AppRoles.Student}' hoặc '{AppRoles.Learner}'."
                });
            }

            try
            {
                return Ok(await _assignmentResponseService.UpdateAssigmentResponseByIdAsync(
                    topicId, id, dto, callerId, role, ct));
            }
            catch (Exception ex)
            {
                return HandleAssignmentException(ex);
            }
        }

        [HttpPut("{id:guid}/grade")]
        public async Task<ActionResult<AssignmentResponseDTO>> GradeAssignmentResponse(
            [FromRoute] Guid topicId,
            [FromRoute] Guid id,
            [FromBody] GradeAssignmentResponseRequest request,
            CancellationToken ct = default)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            if (!IsRole(role, AppRoles.Admin) && !IsRole(role, AppRoles.Teacher)) return Forbid();

            try
            {
                return Ok(await _assignmentResponseService.GradeAssignmentResponseAsync(
                    topicId, id, request, callerId, role, ct));
            }
            catch (Exception ex)
            {
                return HandleAssignmentException(ex);
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteAssignmentResponseById(
            [FromRoute] Guid topicId,
            [FromRoute] Guid id,
            CancellationToken ct = default)
        {
            if (!TryGetCaller(out var callerId, out var role)) return Unauthorized();
            if (!IsRole(role, AppRoles.Student)) return Forbid();

            try
            {
                await _assignmentResponseService.DeleteAssigmentResponseAsync(topicId, id, callerId, role, ct);
                return NoContent();
            }
            catch (Exception ex)
            {
                return HandleAssignmentException(ex);
            }
        }

        private bool TryGetCaller(out Guid callerId, out string? role)
        {
            var id = User.FindFirstValue("userID") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            role = User.FindFirstValue(ClaimTypes.Role);
            return Guid.TryParse(id, out callerId) && callerId != Guid.Empty;
        }

        private static bool IsRole(string? actual, string expected) =>
            expected == AppRoles.Student
                ? AppRoles.IsStudent(actual)
                : string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        private ActionResult HandleAssignmentException(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message }),
                KeyNotFoundException => NotFound(new { message = exception.Message }),
                ArgumentException => BadRequest(new { message = exception.Message }),
                DbUpdateConcurrencyException => Conflict(new { message = "Assignment response changed concurrently. Reload it and retry." }),
                InvalidOperationException => Conflict(new { message = exception.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = "Assignment response operation failed." })
            };
        }
    }
}
