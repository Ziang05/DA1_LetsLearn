using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IAssignmentResponseService
    {
        Task<AssignmentResponseDTO> GetAssigmentResponseByIdAsync(Guid id);
        Task<AssignmentResponseDTO> CreateAssigmentResponseAsync(CreateAssignmentResponseRequest dto, Guid studentId);
        Task<IEnumerable<AssignmentResponseDTO>> GetAllAssigmentResponseByTopicIdAsync(Guid topicId);
        Task<AssignmentResponseDTO> UpdateAssigmentResponseByIdAsync(Guid id, UpdateAssignmentResponseRequest dto);
        Task DeleteAssigmentResponseAsync(Guid id);
    }
}
