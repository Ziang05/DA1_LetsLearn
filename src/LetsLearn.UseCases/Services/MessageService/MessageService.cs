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
                var courseIdStr = dto.ConversationId.ToString();
                var course = await _unitOfWork.Course.GetByIdAsync(courseIdStr);
                if (course != null)
                {
                    conversation = new Conversation
                    {
                        Id = dto.ConversationId,
                        User1Id = course.CreatorId,
                        User2Id = dto.ConversationId, // Using Id as sentinel to bypass unique index (User1Id, User2Id)
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Conversations.AddAsync(conversation);
                }
                else
                {
                    throw new KeyNotFoundException($"Conversation or Course not found for ID: {dto.ConversationId}");
                }
            }
            else
            {
                conversation.UpdatedAt = DateTime.UtcNow;
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
            catch (Exception ex)
            {
                // Catching all to provide better context, though DbUpdateException is the usual suspect
                throw new InvalidOperationException($"Failed to save chat message: {ex.Message}", ex);
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
            if (conversation != null)
            {
                if (conversation.User1Id == userId || conversation.User2Id == userId)
                {
                    return true;
                }
                
                // If User2Id is the sentinel (Empty or equal to Id), this is a Course Group Chat!
                if (conversation.User2Id == Guid.Empty || conversation.User2Id == conversation.Id)
                {
                    return await _unitOfWork.Enrollments.ExistsAsync(e => e.CourseId == conversationId.ToString() && e.StudentId == userId);
                }
                
                return false;
            }

            // Fallback: Check if the conversationId is actually a CourseId (for Course Group Chat that doesn't have a Conversation record yet)
            var course = await _unitOfWork.Course.GetByIdAsync(conversationId.ToString());
            if (course != null)
            {
                // Check if user is the creator or enrolled in the course
                if (course.CreatorId == userId) return true;
                return await _unitOfWork.Enrollments.ExistsAsync(e => e.CourseId == conversationId.ToString() && e.StudentId == userId);
            }

            return false;
        }
    }
}
