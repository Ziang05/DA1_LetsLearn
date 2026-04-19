using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class Conversation
    {
        public Guid Id { get; set; }          // UUID PK
        public Guid User1Id { get; set; }     // UUID FK -> users.id
        public Guid User2Id { get; set; }     // UUID FK -> users.id
        public DateTime? UpdatedAt { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();

    }

}
