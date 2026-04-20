using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class ConversationController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        public ConversationController(IConversationService conversationService)
        {
            _conversationService = conversationService;
        }

        [HttpGet]
        public async Task<ActionResult<List<ConversationDTO>>> GetConversations()
        {
            var userId = Guid.Parse(User.FindFirst("userID")?.Value ?? throw new UnauthorizedAccessException("User ID not found"));
            var conversations = await _conversationService.GetAllByUserIdAsync(userId);
            return Ok(conversations);
        }

        [HttpPost]
        public async Task<ActionResult<ConversationDTO>> CreateOrGetConversation([FromQuery] Guid otherUserId)
        {
            var userId = Guid.Parse(User.FindFirst("userID")?.Value ?? throw new UnauthorizedAccessException("User ID not found"));
            var conversation = await _conversationService.CreateConversationAsync(userId, otherUserId);
            return Ok(conversation);
        }
    }
}
