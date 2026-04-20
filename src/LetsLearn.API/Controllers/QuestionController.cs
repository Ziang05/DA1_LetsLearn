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

namespace LetsLearn.WebApi.Controllers
{
    [ApiController]
    [Route("question")]
    [Authorize]
    public class QuestionController : ControllerBase
    {
        private readonly IQuestionService _service;
        private readonly ILogger<QuestionController> _logger;

        public QuestionController(IQuestionService service, ILogger<QuestionController> logger)
        {
            _service = service;
            _logger = logger;
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
                var created = await _service.CreateAsync(request, userId, ct);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // GET: question?courseId=CS01
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<GetQuestionResponse>>> GetQuestions(
            [FromQuery] string? courseId = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(courseId))
                return BadRequest(new { message = "CourseId query parameter is required." });

            try
            {
                var list = await _service.GetByCourseIdAsync(courseId, ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // GET: api/question/{id}
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<GetQuestionResponse>> GetById(Guid id, CancellationToken ct)
        {
            try
            {
                var data = await _service.GetByIdAsync(id, ct);
                return Ok(data);
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
        [AllowAnonymous]
        public async Task<ActionResult<List<GetQuestionResponse>>> GetByCourse(String courseId, CancellationToken ct)
        {
            try
            {
                var list = await _service.GetByCourseIdAsync(courseId, ct);
                return Ok(list);
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
                var updated = await _service.UpdateAsync(request, userId, ct);
                _logger.LogInformation("Question {QuestionId} updated successfully", updated.Id);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Question {QuestionId} not found for update", questionId);
                return NotFound(new { message = "Question not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating question {QuestionId}", questionId);
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST: api/question/bulk-import
        [HttpPost("bulk-import")]
        [AllowAnonymous]
        public async Task<ActionResult> BulkImport(IFormFile file, [FromQuery] string courseId, CancellationToken ct)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userID")?.Value;
            Guid userId = string.IsNullOrEmpty(userIdClaim) ? Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6") : Guid.Parse(userIdClaim);

            try
            {
                var resultCount = await _service.ImportBulkQuestionsAsync(file, courseId, userId, ct);
                return Ok(new { message = $"Import {resultCount} questions successfully!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private Guid GetUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }
    }
}
