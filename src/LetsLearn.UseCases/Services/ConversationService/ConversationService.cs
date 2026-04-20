using LetsLearn.Core.Interfaces;
using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace LetsLearn.UseCases.Services.ConversationService
{
    public class ConversationService : IConversationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ConversationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if user1 == null: +1
        // - if user2 == null: +1
        // - logical operator || (user1 OR user2 missing): +1
        // - if existingConversation != null: +1
        // - DbUpdateException when committing: +1 (optional branch)
        // D = 5 => Minimum Test Cases = D + 1 = 6
        public async Task<ConversationDTO> CreateConversationAsync(Guid user1Id, Guid user2Id)
        {
            var user1 = await _unitOfWork.Users.GetByIdAsync(user1Id);
            var user2 = await _unitOfWork.Users.GetByIdAsync(user2Id);
            if (user1 == null || user2 == null)
            {
                throw new ArgumentException("One or both users do not exist.");
            }

            var existingConversation = await _unitOfWork.Conversations.FindByUsersAsync(user1Id, user2Id);
            if (existingConversation != null)
            {
                return MapToDTO(existingConversation);
            }

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                User1Id = user1Id,
                User2Id = user2Id,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Conversations.AddAsync(conversation);
            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to create conversation.", ex);
            }
            return MapToDTO(conversation);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if user not found (ExistsAsync false): +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<IEnumerable<ConversationDTO>> GetAllByUserIdAsync(Guid userId)
        {
            if (!await _unitOfWork.Users.ExistsAsync(u => u.Id == userId))
            {
                throw new ArgumentException("User not found.");
            }

            var conversations = await _unitOfWork.Conversations.FindAllByUserIdAsync(userId);
            return conversations.Select(MapToDTO).ToList();
        }

        private ConversationDTO MapToDTO(Conversation conversation)
        {
            return new ConversationDTO
            {
                Id = conversation.Id,
                User1Id = conversation.User1Id,
                User2Id = conversation.User2Id,
                UpdatedAt = conversation.UpdatedAt
            };
        }
    }
}
