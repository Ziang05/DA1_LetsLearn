using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class Message
    {
        public Guid Id { get; set; }              // UUID PK
        public Guid ConversationId { get; set; }  // UUID FK -> conversations.id
        public Guid SenderId { get; set; }        // UUID FK -> users.id
        public string Content { get; set; } = null!;  // VARCHAR(1000) NOT NULL
        public DateTime Timestamp { get; set; }   // NOT NULL
    }
}
