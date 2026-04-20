using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.DTOs
{
    public class SectionDTO
    {
        public Guid Id { get; set; }

        public int Position { get; set; }

        [Required(ErrorMessage = "Title cannot be empty")]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public Guid CourseId { get; set; }

        public List<TopicDTO>? Topics { get; set; }
    }

    public class SectionResponse
    {
        public Guid Id { get; set; }
        public string CourseId { get; set; } = null!;
        public int? Position { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<TopicResponse> Topics { get; set; } = new();
    }

    public class CreateSectionRequest
    {
        public string CourseId { get; set; } = null!;
        public int? Position { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateSectionRequest
    {
        public Guid Id { get; set; }
        public int? Position { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<TopicUpsertDTO>? Topics { get; set; }
    }
}
