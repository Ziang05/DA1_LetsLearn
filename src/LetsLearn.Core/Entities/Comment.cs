using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class Comment
    {
        public Guid Id { get; set; }              // UUID PK
        public Guid UserId { get; set; }          // UUID FK -> users.id
        public Guid TopicId { get; set; }         // UUID FK -> topics.id
        public string Text { get; set; } = null!; // VARCHAR(1000) NOT NULL
        public DateTime CreatedAt { get; set; }   // NOT NULL
        public Guid? ParentCommentId { get; set; }
        public Guid? RootCommentId { get; set; }
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();

    }
}
