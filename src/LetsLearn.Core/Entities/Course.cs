using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace LetsLearn.Core.Entities
{
    public class Course
    {
        // columns
        public String Id { get; set; } = null!;        //PK
        public Guid CreatorId { get; set; }          // UUID FK -> users.id
        public string Title { get; set; } = null!;   // UNIQUE
        public string? Description { get; set; }
        public int TotalJoined { get; set; } = 0;    // DEFAULT 0 UPDATE THIS COUNTER IN ENROLLMENT TRANSACTION SERVICE
        public string? ImageUrl { get; set; }
        public decimal? Price { get; set; }
        public string? Category { get; set; }
        public string? Level { get; set; }
        public bool IsPublished { get; set; } = false; 

        // navigation
        public ICollection<Section> Sections { get; set; } = new List<Section>();
    }
}
