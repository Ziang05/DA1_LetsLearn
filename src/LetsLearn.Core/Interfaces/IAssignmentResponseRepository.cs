using LetsLearn.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Interfaces
{
    public interface IAssignmentResponseRepository : IRepository<AssignmentResponse>
    {
        Task<IEnumerable<AssignmentResponse>> GetAllByTopicIdAsync(Guid topicId);
        Task<IEnumerable<AssignmentResponse>> GetAllByStudentIdAsync(Guid studentId);
        Task<AssignmentResponse?> GetByTopicIdAndStudentIdAsync(Guid topicId, Guid studentId);
        Task<AssignmentResponse?> GetByTopicIdAndStudentIdWithFilesAsync(Guid topicId, Guid studentId);
        Task<IEnumerable<AssignmentResponse>> GetByTopicIdsAndStudentIdAsync(IEnumerable<Guid> topicIds, Guid studentId);
        Task<AssignmentResponse?> GetByIdWithFilesAsync(Guid id);
        Task<IEnumerable<AssignmentResponse>> GetAllByTopicIdWithFilesAsync(Guid topicId);
        Task<AssignmentResponse?> GetByIdTrackedWithFilesAsync(Guid id);
        Task<IReadOnlyList<AssignmentResponse>> FindByTopicIdsAndStudentIdAsync(IEnumerable<Guid> topicIds, Guid studentId, CancellationToken ct = default);
    }
}
