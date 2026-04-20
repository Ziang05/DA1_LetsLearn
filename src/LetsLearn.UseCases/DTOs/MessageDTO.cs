using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class CreateMessageRequest
    {
        public Guid ConversationId { get; set; }
        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; } = null!;
    }

    public class GetMessageRequest
    {
        public Guid ConversationId { get; set; }
    }

    public class GetMessageResponse
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}
