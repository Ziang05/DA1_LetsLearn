using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using LetsLearn.Core.Entities;

namespace LetsLearn.API.Controllers
{
    [ApiController]
    [Route("message")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IMessageService _messageService;

        public ChatController(IMessageService messageService)
        {
            _messageService = messageService;
        }

        [HttpPost("sendMessages")]
        public async Task<IActionResult> SendMessage([FromBody] CreateMessageRequest createMessageDto)
        {
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);
            if (!await _messageService.IsUserInConversationAsync(userId, createMessageDto.ConversationId))
            {
                return Forbid("You do not have access to this conversation.");
            }
            await _messageService.CreateMessageAsync(createMessageDto, userId);
            return Ok();
        }

        [HttpPost("getMessages")]
        public async Task<ActionResult<IEnumerable<GetMessageResponse>>> GetMessagesByConversationId([FromBody] GetMessageRequest requestDto)
        {
            var userId = Guid.Parse(User.Claims.First(c => c.Type == "userID").Value);

            //check
            if (!await _messageService.IsUserInConversationAsync(userId, requestDto.ConversationId))
            {
                return Forbid("You do not have access to this conversation.");
            }

            var messages = await _messageService.GetMessagesByConversationIdAsync(requestDto.ConversationId);
            return Ok(messages);
        }
    }
}
