using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services.CommentService
{
    public class CommentService : ICommentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CommentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if user == null: +1
        // - if topic == null: +1
        // - DbUpdateException CommitAsync (optional): +1
        // D = 3 => Minimum Test Cases = D + 1 = 4
        public async Task<GetCommentResponse> AddCommentAsync(Guid commenterId, CreateCommentRequest dto, CancellationToken ct = default)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(commenterId, ct)
                ?? throw new Exception("Người dùng không tồn tại");

            // Check if Topic exists
            var topic = await _unitOfWork.Topics.GetByIdAsync(dto.TopicId, ct)
                ?? throw new Exception("Chủ đề không tồn tại");

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                UserId = commenterId,
                TopicId = dto.TopicId,
                Text = dto.Text,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Comments.AddAsync(comment);
            await _unitOfWork.CommitAsync();

            return new GetCommentResponse
            {
                Id = comment.Id,
                Text = comment.Text,
                User = new CommentUserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    Avatar = user.Avatar ?? string.Empty
                },
                TopicId = comment.TopicId,
                CreatedAt = comment.CreatedAt
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - No branching logic (always returns list)
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<GetCommentResponse>> GetCommentsByTopicAsync(Guid topicId, CancellationToken ct = default)
        {
            var comments = await _unitOfWork.Comments.FindByTopicIdAsync(topicId, ct);

            // Get unique user IDs
            var userIds = comments.Select(c => c.UserId).Distinct().ToList();

            // Fetch all users in one query
            var users = new List<User>();
            foreach (var userId in userIds)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
                if (user != null)
                {
                    users.Add(user);
                }
            }

            // Map comments with user data
            return comments.Select(c =>
            {
                var user = users.FirstOrDefault(u => u.Id == c.UserId);
                return new GetCommentResponse
                {
                    Id = c.Id,
                    Text = c.Text,
                    User = new CommentUserInfo
                    {
                        Id = user?.Id ?? c.UserId,
                        Username = user?.Username ?? "Unknown User",
                        Avatar = user?.Avatar ?? string.Empty
                    },
                    TopicId = c.TopicId,
                    CreatedAt = c.CreatedAt
                };
            }).ToList();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if comment not found: +1
        // - DbUpdateException CommitAsync (optional): +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default)
        {
            var comment = await _unitOfWork.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct)
                ?? throw new Exception("Bình luận không tồn tại");

            await _unitOfWork.Comments.DeleteAsync(comment);
            await _unitOfWork.CommitAsync();
        }
    }
}
