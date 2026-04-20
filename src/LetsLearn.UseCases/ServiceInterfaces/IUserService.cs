using LetsLearn.UseCases.DTOs;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IUserService
    {
        Task<GetUserResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<UpdateUserResponse> UpdateAsync(Guid id, UpdateUserDTO dto);
        Task<IEnumerable<GetUserResponse>> GetAllAsync(Guid requesterId);
        Task<IEnumerable<TopicDTO>> GetAllWorksOfUserAsync(Guid userId, string? type, DateTime? start, DateTime? end, CancellationToken ct = default);
        Task LeaveCourseAsync(Guid userId, string courseId, CancellationToken ct = default);
        Task<StudentReportDTO> GetStudentReportAsync(Guid userId, String courseId, DateTime? start, DateTime? end, CancellationToken ct = default);
    }
}
