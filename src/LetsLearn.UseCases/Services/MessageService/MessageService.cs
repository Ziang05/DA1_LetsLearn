using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.Core.Entities;
using LetsLearn.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.UseCases.Services.MessageService
{
    public class MessageService : IMessageService
    {
        private readonly IUnitOfWork _unitOfWork;

        public MessageService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if conversation == null: +1
        // - if !userExists: +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task CreateMessageAsync(CreateMessageRequest dto, Guid SenderId)
        {
            var conversation = await _unitOfWork.Conversations.GetByIdAsync(dto.ConversationId);
            if (conversation == null)
            {
                throw new KeyNotFoundException("Conversation not found");
            }

            var userExists = await _unitOfWork.Users.ExistsAsync(u => u.Id == SenderId);
            if (!userExists)
            {
                throw new KeyNotFoundException("Sender not found");
            }

            var message = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = dto.ConversationId,
                SenderId = SenderId,
                Content = dto.Content,
                Timestamp = DateTime.UtcNow
            };

            await _unitOfWork.Messages.AddAsync(message);
            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to create message.", ex);
            }

            conversation.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to update conversation timestamp.", ex);
            }
        }

        // Test Case Estimation:
        // Decision points (D):
        // - No branching here: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<GetMessageResponse>> GetMessagesByConversationIdAsync(Guid conversationId)
        {
            var messages = await _unitOfWork.Messages.GetMessagesByConversationIdAsync(conversationId);
            var dtos = new List<GetMessageResponse>();
            foreach (var msg in messages)
            {
                dtos.Add(new GetMessageResponse
                {
                    Id = msg.Id,
                    ConversationId = msg.ConversationId,
                    SenderId = msg.SenderId, 
                    Content = msg.Content,
                    Timestamp = msg.Timestamp
                });
            }
            return dtos;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if conversation == null: +1
        // - logical operator (||) in membership check: +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task<bool> IsUserInConversationAsync(Guid userId, Guid conversationId)
        {
            var conversation = await _unitOfWork.Conversations.GetByIdAsync(conversationId);
            if (conversation == null)
            {
                return false;
            }

            return conversation.User1Id == userId || conversation.User2Id == userId;
        }
    }
}
