using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services.AssignmentResponseService
{
    public class AssignmentResponseService : IAssignmentResponseService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AssignmentResponseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        private AssignmentResponseDTO ToDto(AssignmentResponse entity)
        {
            return new AssignmentResponseDTO
            {
                Id = entity.Id,
                TopicId = entity.TopicId,
                StudentId = entity.StudentId,
                Data = new AssignmentResponseData
                {
                    SubmittedAt = entity.SubmittedAt,
                    Files = entity.Files.ToList(),
                    Mark = entity.Mark,
                    Note = entity.Note
                }
            };
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if assignmentResponse == null: +1
        // D = 1 => Minimum Test Cases = D + 1 = 2
        public async Task<AssignmentResponseDTO> GetAssigmentResponseByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.AssignmentResponses.GetByIdWithFilesAsync(id);
            if (entity == null)
            {
                throw new Exception("Assignment response not found"); 
            }
            return ToDto(entity);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - foreach files → if files.Count > 0: +1
        // - DbUpdateException CommitAsync: +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task<AssignmentResponseDTO> CreateAssigmentResponseAsync(CreateAssignmentResponseRequest dto, Guid studentId)
        {
            var entity = new AssignmentResponse
            {
                Id = Guid.NewGuid(),
                TopicId = dto.TopicId,
                StudentId = studentId,
                SubmittedAt = dto.SubmittedAt ?? DateTime.UtcNow,
                Note = dto.Note,
                Mark = dto.Mark,
                Files = new List<CloudinaryFile>()
            };

            foreach (var fileDto in dto.CloudinaryFiles)
            {
                var fileEntity = new CloudinaryFile
                {
                    Id = Guid.NewGuid(),
                    Name = fileDto.Name,
                    DisplayUrl = fileDto.DisplayUrl,
                    DownloadUrl = fileDto.DownloadUrl,
                    AssignmentResponseId = entity.Id
                };

                entity.Files.Add(fileEntity);
            }

            await _unitOfWork.AssignmentResponses.AddAsync(entity);
            await _unitOfWork.CloudinaryFiles.AddRangeAsync(entity.Files);
            await _unitOfWork.CommitAsync();

            return ToDto(entity);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - No branching logic
        // D = 0 => Minimum Test Cases = D + 1 = 1
        public async Task<IEnumerable<AssignmentResponseDTO>> GetAllAssigmentResponseByTopicIdAsync(Guid topicId)
        {
            var entities = await _unitOfWork.AssignmentResponses.GetAllByTopicIdWithFilesAsync(topicId);
            return entities.Select(e => ToDto(e));
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if entity == null: +1
        // - if existing files > 0 → DeleteRangeAsync: +1
        // - foreach new files → if new files.Count > 0: +1
        // - DbUpdateException CommitAsync: +1
        // D = 4 => Minimum Test Cases = D + 1 = 5
        public async Task<AssignmentResponseDTO> UpdateAssigmentResponseByIdAsync(Guid id, UpdateAssignmentResponseRequest dto)
        {
            var entity = await _unitOfWork.AssignmentResponses.GetByIdTrackedWithFilesAsync(id);
            if (entity == null)
            {
                throw new Exception("Assignment response not found");
            }

            entity.TopicId = dto.TopicId;
            entity.StudentId = dto.StudentId;
            entity.SubmittedAt = dto.Data.SubmittedAt;
            entity.Note = dto.Data.Note;
            entity.Mark = dto.Data.Mark;

            await _unitOfWork.CloudinaryFiles.DeleteRangeAsync(entity.Files);
            entity.Files.Clear();

            var newFiles = dto.Data.Files;
            foreach (var file in newFiles)
            {
                file.Id = Guid.NewGuid();
                file.AssignmentResponseId = id;
                entity.Files.Add(file);
            }

            await _unitOfWork.CloudinaryFiles.AddRangeAsync(entity.Files);
            await _unitOfWork.CommitAsync();

            return ToDto(entity);
        }

        // Test Case Estimation:
        // Decision points (D):
        // - if entity == null: +1
        // - DbUpdateException CommitAsync: +1
        // D = 2 => Minimum Test Cases = D + 1 = 3
        public async Task DeleteAssigmentResponseAsync(Guid id)
        {
            var entity = await _unitOfWork.AssignmentResponses.GetByIdTrackedWithFilesAsync(id);
            if (entity == null)
            {
                throw new Exception("Assignment response not found");
            }

            await _unitOfWork.AssignmentResponses.DeleteAsync(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
