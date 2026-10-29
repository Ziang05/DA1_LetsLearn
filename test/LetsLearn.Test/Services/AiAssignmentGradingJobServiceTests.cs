using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services.AiAssignmentGrading;
using Microsoft.Extensions.Configuration;
using Moq;

namespace LetsLearn.Test.Services;

public sealed class AiAssignmentGradingJobServiceTests
{
    [Fact]
    public async Task ReusesIdenticalQueuedRequestWithoutConsumingQuotaOrEnqueueingAgain()
    {
        var responseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var existingJob = new AiAssignmentGradingJob
        {
            Id = Guid.NewGuid(),
            AssignmentResponseId = responseId,
            Status = AiAssignmentGradingJobStatuses.Queued,
            CreatedAt = DateTime.UtcNow
        };
        var uow = new Mock<IUnitOfWork>();
        var responses = new Mock<IAssignmentResponseRepository>();
        var rubrics = new Mock<IAssignmentRubricRepository>();
        var assignments = new Mock<ITopicAssignmentRepository>();
        var jobs = new Mock<IAiAssignmentGradingRepository>();
        var queue = new Mock<IAiAssignmentGradingJobQueue>();
        var quota = new Mock<IAiQuotaService>();

        responses.Setup(r => r.GetByIdWithFilesAsync(responseId)).ReturnsAsync(new AssignmentResponse
        {
            Id = responseId,
            StudentId = studentId,
            TopicId = topicId,
            Files = new List<CloudinaryFile> { new() { Id = Guid.NewGuid(), Name = "answer.txt", DownloadUrl = "https://res.cloudinary.com/demo/raw/upload/v1/LetsLearn/answer.txt" } }
        });
        rubrics.Setup(r => r.GetLatestByTopicIdAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(new AssignmentRubric
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            Version = 1,
            MaxScore = 10m,
            Criteria = new List<AssignmentRubricCriterion> { new() { Id = Guid.NewGuid(), Name = "Quality", Description = "Correct answer", MaxScore = 10m } }
        });
        assignments.Setup(r => r.GetByIdAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(new TopicAssignment { TopicId = topicId, Description = "Answer the prompt." });
        jobs.Setup(r => r.GetReusableByRequestHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingJob);
        uow.SetupGet(x => x.AssignmentResponses).Returns(responses.Object);
        uow.SetupGet(x => x.AssignmentRubrics).Returns(rubrics.Object);
        uow.SetupGet(x => x.TopicAssignments).Returns(assignments.Object);
        uow.SetupGet(x => x.AiAssignmentGradingJobs).Returns(jobs.Object);

        var service = new AiAssignmentGradingJobService(uow.Object, queue.Object, quota.Object, Mock.Of<IConfiguration>());

        var first = await service.RequestAsync(responseId, studentId);
        var second = await service.RequestAsync(responseId, studentId);

        Assert.Equal(existingJob.Id, first.JobId);
        Assert.Equal(existingJob.Id, second.JobId);
        Assert.True(first.ReusedExistingJob);
        Assert.True(second.ReusedExistingJob);
        jobs.Verify(r => r.AddAsync(It.IsAny<AiAssignmentGradingJob>()), Times.Never);
        quota.Verify(q => q.CheckAndConsumeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        queue.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
