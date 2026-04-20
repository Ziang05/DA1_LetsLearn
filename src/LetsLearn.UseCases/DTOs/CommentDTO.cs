using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class CreateCommentRequest
    {
        public Guid TopicId { get; set; }
        public string Text { get; set; } = null!;
    }
    public class GetCommentResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = null!;
        public CommentUserInfo User { get; set; } = null!;
        public Guid TopicId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CommentUserInfo
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public string Avatar { get; set; } = null!;
    }
}
