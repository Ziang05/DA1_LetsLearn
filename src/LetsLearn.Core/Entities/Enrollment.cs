using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Entities
{
    public class Enrollment
    {
        public Guid StudentId { get; set; }
        public string CourseId { get; set; } = null!;
        public DateTime JoinDate { get; set; }
    }
}
