using LetsLearn.Core.Entities;
using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services.CourseClone
{
    public class CourseFactory
    {
        public CourseCloneResult CreateFromTemplate(Course source, CloneCourseRequest meta, Guid creatorId)
        {
            if (string.IsNullOrWhiteSpace(meta.NewCourseId))
                throw new ArgumentException("NewCourseId is required.");

            // Create new Course (snapshot + reset runtime)
            var newCourse = new Course
            {
                Id = meta.NewCourseId,
                Title = meta.Title ?? source.Title,
                Description = meta.Description ?? source.Description,
                ImageUrl = meta.ImageUrl ?? source.ImageUrl,
                Category = meta.Category ?? source.Category,
                Level = meta.Level ?? source.Level,
                Price = meta.Price ?? source.Price,
                IsPublished = meta.IsPublished ?? false,
                CreatorId = creatorId,

                // Reset runtime
                TotalJoined = 0,
                Sections = new List<Section>()
            };

            var result = new CourseCloneResult { Course = newCourse };

            foreach (var s in source.Sections ?? new List<Section>())
            {
                var newSectionId = Guid.NewGuid();
                result.SectionIdMap[s.Id] = newSectionId;

                var newSection = new Section
                {
                    Id = newSectionId,
                    CourseId = newCourse.Id,
                    Position = s.Position,
                    Title = s.Title,
                    Description = s.Description,
                    Topics = new List<Topic>()
                };

                foreach (var t in s.Topics ?? new List<Topic>())
                {
                    // Skip meeting topics
                    if (string.Equals(t.Type, "meeting", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var newTopicId = Guid.NewGuid();
                    result.TopicIdMap[t.Id] = newTopicId;

                    newSection.Topics.Add(new Topic
                    {
                        Id = newTopicId,
                        SectionId = newSectionId,
                        Title = t.Title,
                        Type = t.Type
                    });
                }

                newCourse.Sections.Add(newSection);
            }

            result.SectionCount = newCourse.Sections.Count;
            result.TopicCount = newCourse.Sections.Sum(x => x.Topics?.Count ?? 0);
            result.QuestionCount = 0;

            return result;
        }
    }
}
