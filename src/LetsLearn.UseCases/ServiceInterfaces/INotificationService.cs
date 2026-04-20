using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface INotificationService
    {
        Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, CancellationToken ct = default);
        Task<NotificationDto> MarkAsReadAsync(Guid id, bool isRead, CancellationToken ct = default);
        Task DeleteNotificationAsync(Guid id, CancellationToken ct = default);
        Task<NotificationDto> CreateNotificationAsync(Guid userId, string title, string message, CancellationToken ct = default);
        Task NotifyUserAsync(Guid userId, string title, string message, CancellationToken ct = default);
    }
}
