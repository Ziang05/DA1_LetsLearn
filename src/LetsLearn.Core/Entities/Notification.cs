using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }              // UUID PK
        public Guid UserId { get; set; }          // UUID FK -> users.id (người nhận)
        public string Type { get; set; } = null!; // "ASSIGNMENT_POSTED", "COMMENT_REPLY", ...
        public Guid EntityId { get; set; }        // ví dụ: topic_assignment.id hoặc comments.id
        public Guid ActorId { get; set; }         // UUID FK -> users.id (ai tạo sự kiện)
        public DateTime CreatedAt { get; set; }   // DEFAULT now() ở DB
        public DateTime? ReadAt { get; set; }     // NULL nếu chưa đọc
        public string? Data { get; set; }         // JSON payload (string/JSONB tuỳ DB)
    }
}
