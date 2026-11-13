using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.WebApi.Controllers
{
    [ApiController]
    [Route("question")]
    [Authorize]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _service;
        private readonly ILogger<QuestionController> _logger;

        private readonly LetsLearnContext _db;

        public QuestionController(IQuestionService service, ILogger<QuestionController> logger, LetsLearnContext db)
        {
            _service = service;
            _logger = logger;
            _db = db;
        }

        // POST: question
        [HttpPost]
        public async Task<ActionResult<GetQuestionResponse>> Create(
            [FromBody] CreateQuestionRequest request,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            if (userId == Guid.Empty)
                return Unauthorized(new { message = "Invalid or missing user identity." });

            //request.ModifiedById = userId;

            try
            {
                var courseId = request.CourseId ?? request.Course?.Id;
                await RequireCourseOwner(courseId, userId, ct);
                var created = await _service.CreateAsync(request, userId, ct);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // GET: question?courseId=CS01
        [HttpGet]
        public async Task<ActionResult<List<GetQuestionResponse>>> GetQuestions(
            [FromQuery] string? courseId = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(courseId))
                return BadRequest(new { message = "CourseId query parameter is required." });

            try
            {
                await RequireCourseOwner(courseId, UserId(), ct);
                var list = await _service.GetByCourseIdAsync(courseId, ct);
                return Ok(list);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // GET: api/question/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetQuestionResponse>> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var question = await _db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id && q.DeletedAt == null, ct)
                    ?? throw new KeyNotFoundException("Question not found.");
                await RequireCourseOwner(question.CourseId, UserId(), ct);
                var data = await _service.GetByIdAsync(id, ct);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Question not found." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET: api/question/{courseId}
        [HttpGet("{courseId}")]
        public async Task<ActionResult<List<GetQuestionResponse>>> GetByCourse(String courseId, CancellationToken ct)
        {
            try
            {
                await RequireCourseOwner(courseId, UserId(), ct);
                var list = await _service.GetByCourseIdAsync(courseId, ct);
                return Ok(list);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT: api/question/{courseId}
        [HttpPut("{questionId:guid}")]
        public async Task<ActionResult<GetQuestionResponse>> Update(
             Guid questionId,  
             [FromBody] UpdateQuestionRequest request,
             CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            if (userId == Guid.Empty)
                return Unauthorized(new { message = "Invalid or missing user identity." });

            request.Id = questionId;

            _logger.LogDebug("Update request for QuestionId={QuestionId}, CourseId from body={CourseId}",
                questionId, request.CourseId);

            try
            {
                var existing = await _db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == questionId && q.DeletedAt == null, ct)
                    ?? throw new KeyNotFoundException("Question not found.");
                await RequireCourseOwner(existing.CourseId, userId, ct);
                request.CourseId = existing.CourseId;
                var updated = await _service.UpdateAsync(request, userId, ct);
                _logger.LogInformation("Question {QuestionId} updated successfully", updated.Id);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Question {QuestionId} not found for update", questionId);
                return NotFound(new { message = "Question not found." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating question {QuestionId}", questionId);
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST: api/question/bulk-import
        [HttpPost("bulk-import")]
        public async Task<ActionResult> BulkImport(IFormFile file, [FromQuery] string courseId, CancellationToken ct)
        {
            var userId = UserId();

            try
            {
                await RequireCourseOwner(courseId, userId, ct);
                var resultCount = await _service.ImportBulkQuestionsAsync(file, courseId, userId, ct);
                return Ok(new { message = $"Import {resultCount} questions successfully!" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE: question/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var question = await _db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id && q.DeletedAt == null, ct)
                    ?? throw new KeyNotFoundException("Question not found.");
                await RequireCourseOwner(question.CourseId, UserId(), ct);
                _logger.LogInformation("Deleting question {QuestionId}", id);
                var result = await _service.DeleteAsync(id, ct);
                if (!result)
                {
                    _logger.LogWarning("Question {QuestionId} not found for deletion", id);
                    return NotFound(new { message = "Question not found." });
                }

                _logger.LogInformation("Question {QuestionId} deleted successfully", id);
                return Ok(new { message = "Question deleted successfully." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Question not found." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation error deleting question {QuestionId}", id);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting question {QuestionId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to delete question." });
            }
        }

        private Guid GetUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }

        private Guid UserId()
        {
            var value = User.FindFirstValue("userID");
            return Guid.TryParse(value, out var id) && id != Guid.Empty
                ? id : throw new UnauthorizedAccessException("Sign in to manage questions.");
        }

        private async Task RequireCourseOwner(string? courseId, Guid userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(courseId)) throw new ArgumentException("CourseId is required.");
            var ownerId = await _db.Courses.AsNoTracking().Where(c => c.Id == courseId)
                .Select(c => (Guid?)c.CreatorId).SingleOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Course not found.");
            if (ownerId != userId) throw new UnauthorizedAccessException("Only the course teacher can manage its questions.");
        }
    }
}
