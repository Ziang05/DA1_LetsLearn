using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Core.Shared;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services.AiAssignmentGrading;
using Moq;

namespace LetsLearn.Test.Services;

public sealed class AiAssignmentGradingAuthorizationTests
{
    private readonly Guid _topicId = Guid.NewGuid();
    private readonly Guid _responseId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly string _courseId = Guid.NewGuid().ToString();

    [Fact]
    public async Task StudentCannotReadRubric()
    {
        var (service, mocks) = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetRubricAsync(_topicId, _studentId, AppRoles.Student));

        mocks.Rubrics.Verify(r => r.GetLatestByTopicIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TeacherFromAnotherCourseCannotReadRubric()
    {
        var otherTeacherId = Guid.NewGuid();
        var (service, mocks) = CreateService(courseCreatorId: Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetRubricAsync(_topicId, otherTeacherId, AppRoles.Teacher));

        mocks.Rubrics.Verify(r => r.GetLatestByTopicIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StudentCannotReadAiJobDraft()
    {
        var (service, mocks) = CreateService();
        mocks.Jobs.Setup(j => j.GetWithDraftByIdAsync(_jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAssignmentGradingJob { Id = _jobId, AssignmentResponseId = _responseId });
        mocks.Responses.Setup(r => r.GetByIdWithFilesAsync(_responseId))
            .ReturnsAsync(new AssignmentResponse { Id = _responseId, TopicId = _topicId, StudentId = _studentId });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetJobAsync(_jobId, _studentId, AppRoles.Student));
    }

    [Fact]
    public async Task StudentCannotReadAnotherStudentsPublishedGrade()
    {
        var (service, mocks) = CreateService();
        mocks.Responses.Setup(r => r.GetByIdWithFilesAsync(_responseId))
            .ReturnsAsync(new AssignmentResponse
            {
                Id = _responseId,
                TopicId = _topicId,
                StudentId = Guid.NewGuid(),
                Mark = 9m,
                GradedAt = DateTime.UtcNow
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPublishedGradeAsync(_responseId, _studentId, AppRoles.Student));
    }

    private (AiAssignmentGradingReviewService Service, TestMocks Mocks) CreateService(Guid? courseCreatorId = null)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var topics = new Mock<ITopicRepository>();
        var assignments = new Mock<ITopicAssignmentRepository>();
        var sections = new Mock<ISectionRepository>();
        var courses = new Mock<ICourseRepository>();
        var rubrics = new Mock<IAssignmentRubricRepository>();
        var jobs = new Mock<IAiAssignmentGradingRepository>();
        var responses = new Mock<IAssignmentResponseRepository>();

        topics.Setup(r => r.GetByIdAsync(_topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Topic { Id = _topicId, SectionId = Guid.NewGuid(), Type = "assignment" });
        assignments.Setup(r => r.GetByIdAsync(_topicId, It.IsAny<CancellationToken>())).ReturnsAsync(new TopicAssignment { TopicId = _topicId });
        sections.Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Section { CourseId = _courseId });
        courses.Setup(r => r.GetByIdAsync(_courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Course { Id = _courseId, CreatorId = courseCreatorId ?? Guid.NewGuid() });

        unitOfWork.SetupGet(u => u.Topics).Returns(topics.Object);
        unitOfWork.SetupGet(u => u.TopicAssignments).Returns(assignments.Object);
        unitOfWork.SetupGet(u => u.Sections).Returns(sections.Object);
        unitOfWork.SetupGet(u => u.Course).Returns(courses.Object);
        unitOfWork.SetupGet(u => u.AssignmentRubrics).Returns(rubrics.Object);
        unitOfWork.SetupGet(u => u.AiAssignmentGradingJobs).Returns(jobs.Object);
        unitOfWork.SetupGet(u => u.AssignmentResponses).Returns(responses.Object);

        var service = new AiAssignmentGradingReviewService(unitOfWork.Object, Mock.Of<IAiAssignmentGradingJobService>());
        return (service, new TestMocks(rubrics, jobs, responses));
    }

    private sealed record TestMocks(
        Mock<IAssignmentRubricRepository> Rubrics,
        Mock<IAiAssignmentGradingRepository> Jobs,
        Mock<IAssignmentResponseRepository> Responses);
}
