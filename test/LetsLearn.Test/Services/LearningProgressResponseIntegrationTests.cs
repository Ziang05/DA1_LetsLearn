using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using LetsLearn.UseCases.Services.AssignmentResponseService;
using LetsLearn.UseCases.Services.QuizResponseService;
using Moq;

namespace LetsLearn.Test.Services;

public class LearningProgressResponseIntegrationTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ILearningProgressService> _learningProgressService = new();

    [Fact]
    public async Task CreateQuizResponseMarksTopicCompleted()
    {
        var topicId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var courseId = "course-1";
        var studentId = Guid.NewGuid();
        var quizResponses = new Mock<IQuizResponseRepository>();
        var topics = new Mock<ITopicRepository>();
        var sections = new Mock<ISectionRepository>();
        var activityLogs = new Mock<IRepository<LearningActivityLog>>();

        _uow.SetupGet(u => u.QuizResponses).Returns(quizResponses.Object);
        _uow.SetupGet(u => u.Topics).Returns(topics.Object);
        _uow.SetupGet(u => u.Sections).Returns(sections.Object);
        _uow.SetupGet(u => u.LearningActivityLogs).Returns(activityLogs.Object);
        _uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);
        topics.Setup(r => r.GetByIdAsync(topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Topic { Id = topicId, SectionId = sectionId, Title = "Quiz", Type = "quiz" });
        sections.Setup(r => r.GetByIdAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Section { Id = sectionId, CourseId = courseId });
        _learningProgressService
            .Setup(s => s.MarkTopicCompletedAsync(topicId, studentId, "submit_quiz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TopicProgressDto
            {
                TopicId = topicId,
                StudentId = studentId,
                Status = "completed",
                CompletionSource = "submit_quiz"
            });

        var service = new QuizResponseService(_uow.Object, _learningProgressService.Object);

        await service.CreateQuizResponseAsync(new QuizResponseRequest
        {
            TopicId = topicId,
            Data = new QuizResponseData
            {
                Status = "completed",
                CompletedAt = DateTime.UtcNow,
                Answers = new List<QuizResponseAnswerDTO>()
            }
        }, studentId);

        quizResponses.Verify(r => r.AddAsync(It.Is<QuizResponse>(q =>
            q.TopicId == topicId &&
            q.StudentId == studentId)), Times.Once);
        activityLogs.Verify(r => r.AddAsync(It.Is<LearningActivityLog>(log =>
            log.UserId == studentId &&
            log.CourseId == courseId &&
            log.TopicId == topicId &&
            log.EventType == "quiz_submitted")), Times.Once);
        _learningProgressService.Verify(s => s.MarkTopicCompletedAsync(
            topicId,
            studentId,
            "submit_quiz",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAssignmentResponseMarksTopicCompleted()
    {
        var topicId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var courseId = "course-1";
        var studentId = Guid.NewGuid();
        var assignmentResponses = new Mock<IAssignmentResponseRepository>();
        var cloudinaryFiles = new Mock<IRepository<CloudinaryFile>>();
        var topics = new Mock<ITopicRepository>();
        var sections = new Mock<ISectionRepository>();
        var activityLogs = new Mock<IRepository<LearningActivityLog>>();

        _uow.SetupGet(u => u.AssignmentResponses).Returns(assignmentResponses.Object);
        _uow.SetupGet(u => u.CloudinaryFiles).Returns(cloudinaryFiles.Object);
        _uow.SetupGet(u => u.Topics).Returns(topics.Object);
        _uow.SetupGet(u => u.Sections).Returns(sections.Object);
        _uow.SetupGet(u => u.LearningActivityLogs).Returns(activityLogs.Object);
        _uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);
        topics.Setup(r => r.GetByIdAsync(topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Topic { Id = topicId, SectionId = sectionId, Title = "Assignment", Type = "assignment" });
        sections.Setup(r => r.GetByIdAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Section { Id = sectionId, CourseId = courseId });
        _learningProgressService
            .Setup(s => s.MarkTopicCompletedAsync(topicId, studentId, "submit_assignment", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TopicProgressDto
            {
                TopicId = topicId,
                StudentId = studentId,
                Status = "completed",
                CompletionSource = "submit_assignment"
            });

        var service = new AssignmentResponseService(_uow.Object, _learningProgressService.Object);

        await service.CreateAssigmentResponseAsync(new CreateAssignmentResponseRequest
        {
            TopicId = topicId,
            SubmittedAt = DateTime.UtcNow,
            CloudinaryFiles = new List<CreateCloudinaryFileRequest>()
        }, studentId);

        assignmentResponses.Verify(r => r.AddAsync(It.Is<AssignmentResponse>(a =>
            a.TopicId == topicId &&
            a.StudentId == studentId)), Times.Once);
        activityLogs.Verify(r => r.AddAsync(It.Is<LearningActivityLog>(log =>
            log.UserId == studentId &&
            log.CourseId == courseId &&
            log.TopicId == topicId &&
            log.EventType == "assignment_submitted")), Times.Once);
        _learningProgressService.Verify(s => s.MarkTopicCompletedAsync(
            topicId,
            studentId,
            "submit_assignment",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
