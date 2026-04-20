using LetsLearn.UseCases.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IQuizResponseService
    {
        Task<QuizResponseDTO> GetQuizResponseByIdAsync(Guid id, CancellationToken ct = default);
        Task<QuizResponseDTO> CreateQuizResponseAsync(QuizResponseRequest dto, Guid studentId, CancellationToken ct = default);
        Task<IEnumerable<QuizResponseDTO>> GetAllQuizResponsesByTopicIdAsync(Guid topicId, CancellationToken ct = default);
        Task<IEnumerable<QuizResponseDTO>> GetAllQuizResponsesByTopicIdOfStudentAsync(Guid topicId, Guid studentId, CancellationToken ct = default);
        Task<QuizResponseDTO> UpdateQuizResponseByIdAsync(Guid id, QuizResponseRequest dto, CancellationToken ct = default);
    }
}
