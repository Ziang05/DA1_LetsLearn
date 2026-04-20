using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using System.Text.Json;

namespace LetsLearn.UseCases.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _uow;
        private readonly JsonSerializerOptions _jsonOptions;

        public NotificationService(IUnitOfWork uow)
        {
            _uow = uow;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if user == null: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _uow.Users.GetByIdAsync(userId)
                       ?? throw new KeyNotFoundException("User not found");

            var list = await _uow.Notifications.GetByUserIdOrderByCreatedAtDescAsync(userId, ct);

            return list.Select(ToDto).ToList();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if notification == null: +1
        // - isRead == true: +1
        // - isRead == false: +1
        // D = 3 => Minimum Test Cases = D + 1 = 4
        public async Task<NotificationDto> MarkAsReadAsync(Guid id, bool isRead, CancellationToken ct = default)
        {
            var notification = await _uow.Notifications.GetByIdAsync(id)
                              ?? throw new KeyNotFoundException("Notification not found");

            notification.ReadAt = isRead ? DateTime.UtcNow : null;

            await _uow.CommitAsync();

            return ToDto(notification);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if notification == null: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task DeleteNotificationAsync(Guid id, CancellationToken ct = default)
        {
            var notification = await _uow.Notifications.GetByIdAsync(id)
                              ?? throw new KeyNotFoundException("Notification not found");

            await _uow.Notifications.DeleteAsync(notification);
            await _uow.CommitAsync();
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if user == null: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<NotificationDto> CreateNotificationAsync(Guid userId, string title, string message, CancellationToken ct = default)
        {
            var user = await _uow.Users.GetByIdAsync(userId)
                       ?? throw new KeyNotFoundException("User not found");

            var payload = new { title, message };

            var entity = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = "GENERIC",
                EntityId = Guid.Empty,
                ActorId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ReadAt = null,
                Data = JsonSerializer.Serialize(payload, _jsonOptions)
            };

            await _uow.Notifications.AddAsync(entity);
            await _uow.CommitAsync();

            return ToDto(entity);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - Delegates to CreateNotificationAsync: +0
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task NotifyUserAsync(Guid userId, string title, string message, CancellationToken ct = default)
        {
            await CreateNotificationAsync(userId, title, message, ct);
        }

        // ===== Mapping =====
        private NotificationDto ToDto(Notification n)
        {
            string? title = null;
            string? message = null;

            if (!string.IsNullOrWhiteSpace(n.Data))
            {
                try
                {
                    // Try to deserialize with case-insensitive options
                    var obj = JsonSerializer.Deserialize<NotificationPayload>(n.Data, _jsonOptions);
                    title = obj?.Title;
                    message = obj?.Message;
                }
                catch (JsonException ex)
                {
                    // Log the exception for debugging
                    Console.WriteLine($"JSON deserialization failed for notification {n.Id}: {ex.Message}");
                    Console.WriteLine($"JSON Data: {n.Data}");

                    // Try manual parsing as fallback
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(n.Data);
                        if (jsonDoc.RootElement.TryGetProperty("title", out var titleElement))
                        {
                            title = titleElement.GetString();
                        }
                        if (jsonDoc.RootElement.TryGetProperty("message", out var messageElement))
                        {
                            message = messageElement.GetString();
                        }
                    }
                    catch
                    {
                        // Ultimate fallback
                        title = n.Type;
                        message = "Failed to parse notification data";
                    }
                }
            }

            // Use fallback values if parsing failed
            title ??= n.Type;
            message ??= "No message available";

            return new NotificationDto
            {
                Id = n.Id,
                Title = title,
                Message = message,
                Timestamp = n.CreatedAt,
                IsRead = n.ReadAt != null
            };
        }

        private class NotificationPayload
        {
            public string? Title { get; set; }
            public string? Message { get; set; }
        }
    }
}