using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }     // lấy từ Data
        public string? Message { get; set; }   // lấy từ Data
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    public class CreateNotificationRequest
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = default!;
        public string Message { get; set; } = default!;
    }

    public class SetReadRequest
    {
        public bool IsRead { get; set; }
    }
}
