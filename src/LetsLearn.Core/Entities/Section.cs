using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class Section
    {
        // columns
        public Guid Id { get; set; }                 // UUID PK
        public string CourseId { get; set; } = null!;           // UUID FK -> courses.id
        public int? Position { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        // navigation
        public ICollection<Topic> Topics { get; set; } = new List<Topic>();
    }
}
