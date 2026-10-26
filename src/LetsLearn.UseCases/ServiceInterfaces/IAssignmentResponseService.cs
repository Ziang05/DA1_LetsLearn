using LetsLearn.UseCases.DTOs;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IAssignmentResponseService
    {
        Task ValidateStudentCanSubmitAsync(Guid topicId, Guid studentId, CancellationToken ct = default);
        Task<AssignmentResponseDTO> GetAssigmentResponseByIdAsync(
            Guid id, Guid requesterId, string? requesterRole, Guid? routeTopicId = null, CancellationToken ct = default);
        Task<AssignmentResponseDTO> CreateAssigmentResponseAsync(
            CreateAssignmentResponseRequest dto, Guid studentId, CancellationToken ct = default);
        Task<IEnumerable<AssignmentResponseDTO>> GetAllAssigmentResponseByTopicIdAsync(
            Guid topicId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<IEnumerable<AssignmentResponseDTO>> GetAssigmentResponsesByTopicIdAndStudentIdAsync(
            Guid topicId, Guid studentId, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<AssignmentResponseDTO> UpdateAssigmentResponseByIdAsync(
            Guid topicId, Guid id, UpdateAssignmentResponseRequest dto, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task<AssignmentResponseDTO> GradeAssignmentResponseAsync(
            Guid topicId, Guid id, GradeAssignmentResponseRequest request, Guid requesterId, string? requesterRole, CancellationToken ct = default);
        Task DeleteAssigmentResponseAsync(
            Guid topicId, Guid id, Guid requesterId, string? requesterRole, CancellationToken ct = default);
    }
}
