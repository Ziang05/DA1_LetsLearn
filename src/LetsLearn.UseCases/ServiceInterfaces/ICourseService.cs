using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface ICourseService
    {
        Task<CreateCourseResponse> CreateCourseAsync(CreateCourseRequest dto, Guid userId, CancellationToken ct = default);
        Task<UpdateCourseResponse> UpdateCourseAsync(UpdateCourseRequest dto, CancellationToken ct = default);
        Task<IEnumerable<GetCourseResponse>> GetAllCoursesByUserIdAsync(Guid userId, CancellationToken ct = default);
        Task<IEnumerable<GetCourseResponse>> GetAllCoursesAsync(CancellationToken ct = default);
        Task<IEnumerable<GetCourseResponse>> GetAllCoursesForAdminAsync(CancellationToken ct = default);
        Task<GetCourseResponse> GetCourseByIdAsync(string id, CancellationToken ct = default);
        Task AddUserToCourseAsync(string courseId, Guid userId, CancellationToken ct = default);
        Task<IEnumerable<TopicDTO>> GetAllWorksOfCourseAndUserAsync(string courseId, Guid userId, string type, DateTime? start, DateTime? end, CancellationToken ct = default);
        Task<TopicDataDTO?> GetTopicDataByTypeAsync(Guid topicId, Guid userId, DateTime? start, DateTime? end, CancellationToken ct = default);
        Task<AllAssignmentsReportDTO> GetAssignmentsReportAsync(String courseId, DateTime? startTime, DateTime? endTime, CancellationToken ct = default);
        Task<AllQuizzesReportDTO> GetQuizzesReportAsync(String courseId, DateTime? startTime, DateTime? endTime, CancellationToken ct = default);
    }
}
