using System.Linq.Expressions;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LetsLearn.Test.Services;

public class LearningProgressServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITopicRepository> _topics = new();
    private readonly Mock<ISectionRepository> _sections = new();
    private readonly Mock<ICourseRepository> _courses = new();
    private readonly Mock<IEnrollmentRepository> _enrollments = new();
    private readonly Mock<ITopicProgressRepository> _topicProgresses = new();
    private readonly Mock<ILearningProgressRepository> _learningProgresses = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IQuizResponseRepository> _quizResponses = new();
    private readonly Mock<IAssignmentResponseRepository> _assignmentResponses = new();
    private readonly Mock<IRepository<LearningActivityLog>> _activityLogs = new();

    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly string _courseId = "course-1";
    private readonly Guid _sectionId = Guid.NewGuid();
    private readonly Guid _topicId = Guid.NewGuid();

    public LearningProgressServiceTests()
    {
        _uow.SetupGet(u => u.Topics).Returns(_topics.Object);
        _uow.SetupGet(u => u.Sections).Returns(_sections.Object);
        _uow.SetupGet(u => u.Course).Returns(_courses.Object);
        _uow.SetupGet(u => u.Enrollments).Returns(_enrollments.Object);
        _uow.SetupGet(u => u.TopicProgresses).Returns(_topicProgresses.Object);
        _uow.SetupGet(u => u.LearningProgresses).Returns(_learningProgresses.Object);
        _uow.SetupGet(u => u.Users).Returns(_users.Object);
        _uow.SetupGet(u => u.QuizResponses).Returns(_quizResponses.Object);
        _uow.SetupGet(u => u.AssignmentResponses).Returns(_assignmentResponses.Object);
        _uow.SetupGet(u => u.LearningActivityLogs).Returns(_activityLogs.Object);
        _uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        _topics.Setup(r => r.GetByIdAsync(_topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Topic { Id = _topicId, SectionId = _sectionId, Title = "Intro", Type = "page" });
        _sections.Setup(r => r.GetByIdAsync(_sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Section { Id = _sectionId, CourseId = _courseId });
        _courses.Setup(r => r.GetByIdAsync(_courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Course { Id = _courseId, Title = "Course" });
        _enrollments.Setup(r => r.GetByIdsAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Enrollment { StudentId = _studentId, CourseId = _courseId, JoinDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) });
        _sections.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Section, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Section { Id = _sectionId, CourseId = _courseId } });
    }

    [Fact]
    public async Task MarkTopicCompletedCreatesTopicAndCourseProgress()
    {
        var secondTopicId = Guid.NewGuid();
        TopicProgress? savedTopicProgress = null;
        LearningProgress? savedLearningProgress = null;

        _topics.Setup(r => r.GetAllBySectionIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Topic>
            {
                new() { Id = _topicId, SectionId = _sectionId, Title = "Intro", Type = "page" },
                new() { Id = secondTopicId, SectionId = _sectionId, Title = "Quiz", Type = "quiz" }
            });
        _topicProgresses.Setup(r => r.GetByStudentAndTopicAsync(_studentId, _topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopicProgress?)null);
        _topicProgresses.Setup(r => r.AddAsync(It.IsAny<TopicProgress>()))
            .Callback<TopicProgress>(p => savedTopicProgress = p)
            .Returns(Task.CompletedTask);
        _topicProgresses.Setup(r => r.CountCompletedByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _topicProgresses.Setup(r => r.GetByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedTopicProgress == null ? new List<TopicProgress>() : new List<TopicProgress> { savedTopicProgress });
        _learningProgresses.Setup(r => r.GetByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LearningProgress?)null);
        _learningProgresses.Setup(r => r.AddAsync(It.IsAny<LearningProgress>()))
            .Callback<LearningProgress>(p => savedLearningProgress = p)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.MarkTopicCompletedAsync(_topicId, _studentId, "submit_quiz");

        Assert.Equal("completed", result.Status);
        Assert.Equal("submit_quiz", result.CompletionSource);
        Assert.NotNull(savedTopicProgress);
        Assert.NotNull(savedLearningProgress);
        Assert.Equal(1, savedLearningProgress.CompletedTopicCount);
        Assert.Equal(2, savedLearningProgress.TotalTopicCount);
        Assert.Equal(50m, savedLearningProgress.ProgressPercent);
    }

    [Fact]
    public async Task MarkTopicViewedDoesNotDowngradeCompletedTopic()
    {
        var existingTopicProgress = new TopicProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            CourseId = _courseId,
            TopicId = _topicId,
            Status = "completed",
            CompletionSource = "submit_assignment",
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var existingLearningProgress = new LearningProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            CourseId = _courseId
        };

        _topics.Setup(r => r.GetAllBySectionIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Topic> { new() { Id = _topicId, SectionId = _sectionId } });
        _topicProgresses.Setup(r => r.GetByStudentAndTopicAsync(_studentId, _topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTopicProgress);
        _topicProgresses.Setup(r => r.CountCompletedByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _topicProgresses.Setup(r => r.GetByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopicProgress> { existingTopicProgress });
        _learningProgresses.Setup(r => r.GetByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLearningProgress);

        var service = CreateService();
        var result = await service.MarkTopicViewedAsync(_topicId, _studentId);

        Assert.Equal("completed", result.Status);
        Assert.Equal("submit_assignment", result.CompletionSource);
        _topicProgresses.Verify(r => r.UpdateAsync(It.Is<TopicProgress>(p => p.Status == "completed")), Times.Once);
        _learningProgresses.Verify(r => r.UpdateAsync(It.Is<LearningProgress>(p => p.ProgressPercent == 100m)), Times.Once);
    }

    [Fact]
    public async Task GetCourseStudentProgressesRecalculatesMissingProgressForCourseTeacher()
    {
        LearningProgress? savedLearningProgress = null;

        _courses.Setup(r => r.GetByIdAsync(_courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Course { Id = _courseId, Title = "Course", CreatorId = _teacherId });
        _enrollments.Setup(r => r.GetAllByCourseIdAsync(_courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Enrollment>
            {
                new() { StudentId = _studentId, CourseId = _courseId, JoinDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) }
            });
        _learningProgresses.Setup(r => r.GetByCourseAsync(_courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LearningProgress>());
        _learningProgresses.Setup(r => r.GetByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LearningProgress?)null);
        _learningProgresses.Setup(r => r.AddAsync(It.IsAny<LearningProgress>()))
            .Callback<LearningProgress>(p => savedLearningProgress = p)
            .Returns(Task.CompletedTask);
        _topics.Setup(r => r.GetAllBySectionIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Topic> { new() { Id = _topicId, SectionId = _sectionId, Title = "Intro", Type = "page" } });
        _topicProgresses.Setup(r => r.CountCompletedByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _topicProgresses.Setup(r => r.GetByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopicProgress>
            {
                new()
                {
                    StudentId = _studentId,
                    CourseId = _courseId,
                    TopicId = _topicId,
                    Status = "completed",
                    UpdatedAt = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)
                }
            });
        _users.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User { Id = _studentId, Username = "Student One", Email = "student@example.com", Role = "Student" }
            });

        var service = CreateService();
        var result = await service.GetCourseStudentProgressesAsync(_courseId, _teacherId, "Teacher");

        var studentProgress = Assert.Single(result);
        Assert.Equal(_studentId, studentProgress.StudentId);
        Assert.Equal("Student One", studentProgress.StudentName);
        Assert.Equal(100m, studentProgress.ProgressPercent);
        Assert.NotNull(savedLearningProgress);
    }

    [Fact]
    public async Task SyncCourseProgressFromResponsesBackfillsQuizAndAssignmentProgress()
    {
        var assignmentTopicId = Guid.NewGuid();
        var existingAssignmentProgress = new TopicProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            CourseId = _courseId,
            TopicId = assignmentTopicId,
            Status = "in_progress",
            UpdatedAt = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        TopicProgress? createdQuizProgress = null;

        _courses.Setup(r => r.GetByIdAsync(_courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Course { Id = _courseId, Title = "Course", CreatorId = _teacherId });
        _enrollments.Setup(r => r.GetAllByCourseIdAsync(_courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Enrollment>
            {
                new() { StudentId = _studentId, CourseId = _courseId, JoinDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) }
            });
        _topics.Setup(r => r.GetAllBySectionIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Topic>
            {
                new() { Id = _topicId, SectionId = _sectionId, Title = "Quiz", Type = "quiz" },
                new() { Id = assignmentTopicId, SectionId = _sectionId, Title = "Assignment", Type = "assignment" }
            });
        _quizResponses.Setup(r => r.FindByTopicIdsAndStudentIdAsync(It.IsAny<List<Guid>>(), _studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuizResponse>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    StudentId = _studentId,
                    TopicId = _topicId,
                    CompletedAt = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)
                }
            });
        _assignmentResponses.Setup(r => r.FindByTopicIdsAndStudentIdAsync(It.IsAny<IEnumerable<Guid>>(), _studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignmentResponse>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    StudentId = _studentId,
                    TopicId = assignmentTopicId,
                    SubmittedAt = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc)
                }
            });
        _topicProgresses.Setup(r => r.GetByStudentAndTopicAsync(_studentId, _topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopicProgress?)null);
        _topicProgresses.Setup(r => r.GetByStudentAndTopicAsync(_studentId, assignmentTopicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAssignmentProgress);
        _topicProgresses.Setup(r => r.AddAsync(It.IsAny<TopicProgress>()))
            .Callback<TopicProgress>(p => createdQuizProgress = p)
            .Returns(Task.CompletedTask);
        _topicProgresses.Setup(r => r.CountCompletedByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _topicProgresses.Setup(r => r.GetByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<TopicProgress>
            {
                createdQuizProgress!,
                existingAssignmentProgress
            }.Where(p => p != null).ToList());
        _learningProgresses.Setup(r => r.GetByStudentAndCourseAsync(_studentId, _courseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearningProgress { Id = Guid.NewGuid(), StudentId = _studentId, CourseId = _courseId });

        var service = CreateService();
        var result = await service.SyncCourseProgressFromResponsesAsync(_courseId, _teacherId, "Teacher");

        Assert.Equal(1, result.StudentCount);
        Assert.Equal(1, result.CreatedTopicProgressCount);
        Assert.Equal(1, result.UpdatedTopicProgressCount);
        Assert.Equal(1, result.RecalculatedProgressCount);
        Assert.NotNull(createdQuizProgress);
        Assert.Equal("completed", createdQuizProgress.Status);
        Assert.Equal("submit_quiz", createdQuizProgress.CompletionSource);
        Assert.Equal("completed", existingAssignmentProgress.Status);
        Assert.Equal("submit_assignment", existingAssignmentProgress.CompletionSource);
    }

    private LearningProgressService CreateService()
    {
        return new LearningProgressService(_uow.Object, NullLogger<LearningProgressService>.Instance);
    }
}
