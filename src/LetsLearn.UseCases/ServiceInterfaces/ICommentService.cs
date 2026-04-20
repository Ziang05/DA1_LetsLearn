using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface ICommentService
    {
        Task<GetCommentResponse> AddCommentAsync(Guid commenterId, CreateCommentRequest dto, CancellationToken ct = default);
        Task<IEnumerable<GetCommentResponse>> GetCommentsByTopicAsync(Guid topicId, CancellationToken ct = default);
        Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default);
    }
}
