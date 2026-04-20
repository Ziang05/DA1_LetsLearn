using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LetsLearn.API.Controllers
{
    [Route("topic/{topicId:guid}/quiz-response")]
    [ApiController]
    [Authorize]
    public class QuizResponseController : ControllerBase
    {
        private readonly IQuizResponseService _quizResponseService;

        public QuizResponseController(IQuizResponseService quizResponseService)
        {
            _quizResponseService = quizResponseService;
        }

        [HttpPost]
        public async Task<ActionResult<QuizResponseDTO>> CreateQuizResponse([FromBody] QuizResponseRequest dto, CancellationToken ct = default)
        {
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            var result = await _quizResponseService.CreateQuizResponseAsync(dto, userId, ct);
            return Ok(result);
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<List<QuizResponseDTO>>> GetAllQuizResponsesByTopicId([FromRoute] Guid topicId, [FromQuery] Guid? studentId, CancellationToken ct = default)
        {
            if (studentId.HasValue)
            {
                return Ok(await _quizResponseService.GetAllQuizResponsesByTopicIdOfStudentAsync(topicId, studentId.Value, ct));
            }
            return Ok(await _quizResponseService.GetAllQuizResponsesByTopicIdAsync(topicId, ct));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<QuizResponseDTO>> GetQuizResponseById([FromRoute] Guid id, CancellationToken ct = default)
        {
            return Ok(await _quizResponseService.GetQuizResponseByIdAsync(id, ct));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<QuizResponseDTO>> UpdateQuizResponseById([FromRoute] Guid id, [FromBody] QuizResponseRequest dto, CancellationToken ct = default)
        {
            return Ok(await _quizResponseService.UpdateQuizResponseByIdAsync(id, dto, ct));
        }
        [HttpGet]
        public async Task<ActionResult<List<QuizResponseDTO>>> GetQuizResponsesByTopic(
            [FromRoute] Guid topicId,
            [FromQuery] Guid? studentId = null,
            CancellationToken ct = default)
        {
            if (studentId.HasValue)
            {
                return Ok(await _quizResponseService.GetAllQuizResponsesByTopicIdOfStudentAsync(topicId, studentId.Value, ct));
            }
            return Ok(await _quizResponseService.GetAllQuizResponsesByTopicIdAsync(topicId, ct));
        }
    }
}
