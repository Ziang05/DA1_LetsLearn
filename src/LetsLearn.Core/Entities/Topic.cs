using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class Topic
    {
        // columns
        public Guid Id { get; set; }                 // UUID PK
        public Guid SectionId { get; set; }          // UUID FK -> sections.id
        public string? Title { get; set; }
        public string? Type { get; set; }
    }
}
