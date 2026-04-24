using System;

namespace LetsLearn.Core.Entities
{
    public class Payment
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string CourseId { get; set; } = null!;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? OrderId { get; set; } // Local Order ID
        public string? TransactionId { get; set; } // VNPay Transaction ID
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
    }
}
