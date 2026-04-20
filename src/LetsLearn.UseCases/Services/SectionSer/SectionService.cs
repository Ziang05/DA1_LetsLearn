using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services.SectionSer
{
    public class SectionService : ISectionService
    {
        private readonly IUnitOfWork _uow;
        //private readonly ITopicService _topicService;

        public SectionService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<SectionResponse> CreateSectionAsync(CreateSectionRequest request, CancellationToken ct = default)
        {
            var section = MapToSectionEntity(request);
            await _uow.Sections.AddAsync(section);

            await _uow.CommitAsync();

            var created = await _uow.Sections.GetByIdWithTopicsAsync(section.Id, ct)
                          ?? throw new InvalidOperationException("Section creation failed unexpectedly.");

            return MapToSectionResponse(created);
        }

        public async Task<SectionResponse> GetSectionByIdAsync(Guid sectionId, CancellationToken ct = default)
        {
            var section = await _uow.Sections.GetByIdWithTopicsAsync(sectionId, ct);
            if (section == null)
                throw new KeyNotFoundException($"Section with id {sectionId} not found.");

            return MapToSectionResponse(section);
        }

        public async Task<SectionResponse> UpdateSectionAsync(UpdateSectionRequest request, CancellationToken ct = default)
        {
            var tracked = await _uow.Sections.GetTrackedByIdAsync(request.Id, ct);
            if (tracked == null)
                throw new KeyNotFoundException($"Section with id {request.Id} not found.");

            ApplySectionUpdate(request, tracked);

            var incoming = request.Topics ?? new List<TopicUpsertDTO>();
            var incomingIds = incoming.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();

            // Xoá topics không có trong DTO
            var toDelete = tracked.Topics.Where(t => !incomingIds.Contains(t.Id)).ToList();
            if (toDelete.Count > 0)
                await _uow.Topics.DeleteRangeAsync(toDelete);

            // Upsert topic
            foreach (var up in incoming)
            {
                if (up.Id == null)
                {
                    await _uow.Topics.AddAsync(MapToTopicEntity(tracked.Id, up));
                }
                else
                {
                    var exist = tracked.Topics.FirstOrDefault(t => t.Id == up.Id.Value);
                    if (exist == null)
                    {
                        await _uow.Topics.AddAsync(MapToTopicEntity(tracked.Id, up));
                    }
                    else
                    {
                        ApplyTopicUpdate(up, exist);
                        exist.SectionId = tracked.Id;
                    }
                }
            }

            await _uow.CommitAsync();

            var updated = await _uow.Sections.GetByIdWithTopicsAsync(request.Id, ct)
                          ?? throw new InvalidOperationException("Section update failed unexpectedly.");

            return MapToSectionResponse(updated);
        }

        private static Section MapToSectionEntity(CreateSectionRequest req) => new()
        {
            Id = Guid.NewGuid(),
            CourseId = req.CourseId,
            Position = req.Position,
            Title = req.Title,
            Description = req.Description
        };

        private static void ApplySectionUpdate(UpdateSectionRequest req, Section entity)
        {
            entity.Position = req.Position;
            entity.Title = req.Title;
            entity.Description = req.Description;
        }

        private static Topic MapToTopicEntity(Guid sectionId, TopicUpsertDTO up) => new()
        {
            Id = up.Id ?? Guid.NewGuid(),
            SectionId = sectionId,
            Title = up.Title,
            Type = up.Type
        };

        private static void ApplyTopicUpdate(TopicUpsertDTO up, Topic entity)
        {
            entity.Title = up.Title;
            entity.Type = up.Type;
        }

        private static SectionResponse MapToSectionResponse(Section s) => new()
        {
            Id = s.Id,
            CourseId = s.CourseId,
            Position = s.Position,
            Title = s.Title,
            Description = s.Description,
            Topics = s.Topics.Select(MapToTopicDTO).ToList()
        };

        private static TopicResponse MapToTopicDTO(Topic t) => new()
        {
            Id = t.Id,
            SectionId = t.SectionId,
            Title = t.Title,
            Type = t.Type
        };
    }
}
