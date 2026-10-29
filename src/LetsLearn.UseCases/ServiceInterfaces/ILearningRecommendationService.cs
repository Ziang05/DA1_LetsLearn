using LetsLearn.UseCases.DTOs;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface ILearningRecommendationService
    {
        Task<LearningRecommendationsResponseDto> GetMyRecommendationsAsync(
            string courseId,
            Guid studentId,
            int limit = 3,
            CancellationToken ct = default);
    }
}
