using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface ITopicService
    {
        Task<TopicResponse> CreateTopicAsync(CreateTopicRequest topicRequest, CancellationToken ct = default);
        Task<TopicResponse> UpdateTopicAsync(UpdateTopicRequest topicRequest, CancellationToken ct = default);
        Task<bool> DeleteTopicAsync(Guid id, CancellationToken ct = default);
        Task<TopicResponse> GetTopicByIdAsync(Guid id, CancellationToken ct = default);
        Task<SingleQuizReportDTO> GetSingleQuizReportAsync(String courseId, Guid topicId, CancellationToken ct = default);
        Task<SingleAssignmentReportDTO> GetSingleAssignmentReportAsync(String courseId, Guid topicId, CancellationToken ct = default);
        Task<bool> SaveMeetingHistoryAsync(Guid topicId, SaveMeetingHistoryRequest request, CancellationToken ct = default);

    }
}
