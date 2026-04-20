using LetsLearn.UseCases.DTOs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IQuestionService
    {
        Task<GetQuestionResponse> CreateAsync(CreateQuestionRequest req, Guid userId, CancellationToken ct = default);
        Task<GetQuestionResponse> UpdateAsync(UpdateQuestionRequest req, Guid userId, CancellationToken ct = default);
        Task<GetQuestionResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<GetQuestionResponse>> GetByCourseIdAsync(string courseId, CancellationToken ct = default);
        Task<int> BulkCreateAsync(List<CreateQuestionRequest> requests, Guid userId, CancellationToken ct = default);
        Task<int> ImportBulkQuestionsAsync(IFormFile file, string courseId, Guid userId, CancellationToken ct);
    }
}
