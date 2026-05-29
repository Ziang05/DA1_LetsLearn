using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("ai/chat")]
    [Authorize]
    public class AiChatController : ControllerBase
    {
        private readonly IAiChatService _aiChatService;
        private readonly ILogger<AiChatController> _logger;

        public AiChatController(IAiChatService aiChatService, ILogger<AiChatController> logger)
        {
            _aiChatService = aiChatService;
            _logger = logger;
        }

        [HttpPost("summarize")]
        public async Task<IActionResult> Summarize([FromBody] SummarizeChatRequest request, CancellationToken ct)
        {
            try
            {
                var summary = await _aiChatService.SummarizeChatAsync(request.ConversationId, request.Limit, ct);
                return Ok(new { summary });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tóm tắt chat thất bại.");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("generate-quiz")]
        public async Task<IActionResult> GenerateQuiz([FromBody] GenerateQuizFromChatRequest request, CancellationToken ct)
        {
            try
            {
                var topicId = await _aiChatService.GenerateQuizFromSharedDocumentAsync(
                    request.MessageId,
                    request.BloomLevel,
                    request.QuestionCount,
                    GetUserId(),
                    ct);

                return Ok(new { topicId, message = "Sinh Quiz từ tài liệu thành công và đã thông báo tới nhóm." });
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
                _logger.LogWarning(ex, "Sinh Quiz từ chat thất bại.");
                return BadRequest(new { message = ex.Message });
            }
        }

        private Guid GetUserId()
        {
            var id = User.Claims.FirstOrDefault(c => c.Type == "userID")?.Value
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(id, out var userId) || userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Danh tính người dùng không hợp lệ hoặc bị thiếu.");
            }

            return userId;
        }
    }

    public class SummarizeChatRequest
    {
        public Guid ConversationId { get; set; }
        public int Limit { get; set; } = 50;
    }

    public class GenerateQuizFromChatRequest
    {
        public Guid MessageId { get; set; }
        public string BloomLevel { get; set; } = "Understand";
        public int QuestionCount { get; set; } = 5;
    }
}
