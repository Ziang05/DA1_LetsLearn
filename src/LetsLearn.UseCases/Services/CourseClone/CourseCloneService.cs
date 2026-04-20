using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services.CourseClone
{
    public class CourseCloneService : ICourseCloneService
    {
        private readonly IUnitOfWork _uow;
        private readonly CourseFactory _factory;

        public CourseCloneService(IUnitOfWork uow, CourseFactory factory)
        {
            _uow = uow;
            _factory = factory;
        }

        public async Task<CloneCourseResponse> CloneAsync(
            string sourceCourseId,
            CloneCourseRequest request,
            Guid userId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sourceCourseId))
                throw new ArgumentException("sourceCourseId is required.");

            if (string.IsNullOrWhiteSpace(request.NewCourseId))
                throw new ArgumentException("NewCourseId is required.");

            // 1) Load source course
            var source = await _uow.Course.GetByIdAsync(sourceCourseId, ct)
                         ?? throw new KeyNotFoundException("Source course not found.");

            if (source.Sections == null)
                throw new InvalidOperationException("Source course sections not loaded.");

            // 2) Permission: Creator + Admin
            await EnsureCanCloneAsync(source, userId, ct);

            // 3) New course id must not exist
            var newIdExists = await _uow.Course.ExistsAsync(c => c.Id == request.NewCourseId, ct);
            if (newIdExists)
                throw new InvalidOperationException($"Course ID '{request.NewCourseId}' already exists.");

            // 4) Create skeleton course graph
            var clone = _factory.CreateFromTemplate(source, request, userId);

            // 5) Persist skeleton
            await _uow.Course.AddAsync(clone.Course);

            // 6) Auto enroll creator as student
            await AddCreatorEnrollmentAsync(clone.Course.Id, userId, ct);

            // 7) Clone question bank from old course to new course
            await CloneQuestionBankAsync(sourceCourseId, clone.Course.Id, clone.QuestionIdMap, userId, ct);

            // 8) Clone topic data snapshot (quiz/assignment/page/link/file)
            await CloneTopicDataAsync(source, clone.TopicIdMap, ct);

            // 9) Commit
            try
            {
                await _uow.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Failed to clone course.", ex);
            }

            return new CloneCourseResponse
            {
                Id = clone.Course.Id,
                SourceCourseId = sourceCourseId,
                SectionCount = clone.SectionCount,
                TopicCount = clone.TopicCount
            };
        }

        private async Task AddCreatorEnrollmentAsync(
            string courseId,
            Guid userId,
            CancellationToken ct)
        {
            // GMT+7 giống CreateCourseAsync
            var utcNow = DateTime.UtcNow;
            var gmtPlus7 = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var joinDate = TimeZoneInfo.ConvertTimeFromUtc(utcNow, gmtPlus7);

            var enrollment = new Enrollment
            {
                StudentId = userId,
                CourseId = courseId,
                JoinDate = DateTime.SpecifyKind(joinDate, DateTimeKind.Utc)
            };

            await _uow.Enrollments.AddAsync(enrollment);
        }

        private async Task EnsureCanCloneAsync(Course source, Guid userId, CancellationToken ct)
        {
            if (source.CreatorId == userId) return;

            var user = await _uow.Users.GetByIdAsync(userId, ct)
                       ?? throw new KeyNotFoundException("User not found.");

            var isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            if (!isAdmin)
                throw new InvalidOperationException("You don't have permission to clone this course.");
        }

        private async Task CloneTopicDataAsync(
            Course source,
            Dictionary<Guid, Guid> topicIdMap,
            CancellationToken ct)
        {
            foreach (var sec in source.Sections ?? new List<Section>())
            {
                foreach (var topic in sec.Topics ?? new List<Topic>())
                {
                    // Skip meeting
                    if (string.Equals(topic.Type, "meeting", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!topicIdMap.TryGetValue(topic.Id, out var newTopicId))
                        continue;

                    switch (topic.Type?.ToLowerInvariant())
                    {
                        case "quiz":
                            await CloneQuizSnapshotAsync(topic.Id, newTopicId, ct);
                            break;

                        case "assignment":
                            await CloneAssignmentSnapshotAsync(topic.Id, newTopicId, ct);
                            break;

                        case "page":
                            await CloneTopicPageAsync(topic.Id, newTopicId, ct);
                            break;

                        case "link":
                            await CloneTopicLinkAsync(topic.Id, newTopicId, ct);
                            break;

                        case "file":
                            await CloneTopicFileAsync(topic.Id, newTopicId, ct);
                            break;

                        default:
                            // Unknown: skeleton topic already cloned
                            break;
                    }
                }
            }
        }

        private async Task CloneQuizSnapshotAsync(Guid oldTopicId, Guid newTopicId, CancellationToken ct)
        {
            var oldQuiz = await _uow.TopicQuizzes.GetWithQuestionsAsync(oldTopicId);
            if (oldQuiz == null) return;

            var newQuiz = new TopicQuiz
            {
                TopicId = newTopicId,
                Description = oldQuiz.Description,
                Open = oldQuiz.Open,
                Close = oldQuiz.Close,
                TimeLimit = oldQuiz.TimeLimit,
                TimeLimitUnit = oldQuiz.TimeLimitUnit,
                GradeToPass = oldQuiz.GradeToPass,
                GradingMethod = oldQuiz.GradingMethod,
                AttemptAllowed = oldQuiz.AttemptAllowed,
                Questions = new List<TopicQuizQuestion>()
            };

            foreach (var q in oldQuiz.Questions ?? new List<TopicQuizQuestion>())
            {
                var newQuestionId = Guid.NewGuid();

                var newQ = new TopicQuizQuestion
                {
                    Id = newQuestionId,
                    TopicQuizId = newTopicId,
                    QuestionName = q.QuestionName,
                    QuestionText = q.QuestionText,
                    Type = q.Type,
                    DefaultMark = q.DefaultMark,
                    FeedbackOfTrue = q.FeedbackOfTrue,
                    FeedbackOfFalse = q.FeedbackOfFalse,
                    CorrectAnswer = q.CorrectAnswer,
                    Multiple = q.Multiple,
                    Choices = new List<TopicQuizQuestionChoice>()
                };

                foreach (var c in q.Choices ?? new List<TopicQuizQuestionChoice>())
                {
                    newQ.Choices.Add(new TopicQuizQuestionChoice
                    {
                        Id = Guid.NewGuid(),
                        QuizQuestionId = newQuestionId,
                        Text = c.Text,
                        GradePercent = c.GradePercent,
                        Feedback = c.Feedback
                    });
                }

                newQuiz.Questions.Add(newQ);
            }

            await _uow.TopicQuizzes.AddAsync(newQuiz);
        }

        private async Task CloneAssignmentSnapshotAsync(Guid oldTopicId, Guid newTopicId, CancellationToken ct)
        {
            var oldAssignment = (await _uow.TopicAssignments.FindAsync(a => a.TopicId == oldTopicId, ct))
                .FirstOrDefault();
            if (oldAssignment == null) return;

            var newAssignment = new TopicAssignment
            {
                TopicId = newTopicId,
                Description = oldAssignment.Description,
                Open = oldAssignment.Open,
                Close = oldAssignment.Close,
                MaximumFile = oldAssignment.MaximumFile,
                MaximumFileSize = oldAssignment.MaximumFileSize,
                RemindToGrade = oldAssignment.RemindToGrade,
                Files = new List<CloudinaryFile>()
            };

            // Clone attached template files (NOT response files)
            foreach (var f in oldAssignment.Files ?? new List<CloudinaryFile>())
            {
                newAssignment.Files.Add(new CloudinaryFile
                {
                    Id = Guid.NewGuid(),
                    Name = f.Name,
                    DisplayUrl = f.DisplayUrl,
                    DownloadUrl = f.DownloadUrl,

                    // Reset runtime links
                    AssignmentResponseId = null,
                    TopicFileId = null,

                    // Link to new assignment
                    TopicAssignmentId = newTopicId
                });
            }

            await _uow.TopicAssignments.AddAsync(newAssignment);
        }

        private async Task CloneTopicPageAsync(Guid oldTopicId, Guid newTopicId, CancellationToken ct)
        {
            var oldPage = (await _uow.TopicPages.FindAsync(p => p.TopicId == oldTopicId, ct)).FirstOrDefault();
            if (oldPage == null) return;

            var newPage = new TopicPage
            {
                TopicId = newTopicId,
                Description = oldPage.Description,
                Content = oldPage.Content
            };

            await _uow.TopicPages.AddAsync(newPage);
        }

        private async Task CloneTopicLinkAsync(Guid oldTopicId, Guid newTopicId, CancellationToken ct)
        {
            var oldLink = (await _uow.TopicLinks.FindAsync(l => l.TopicId == oldTopicId, ct)).FirstOrDefault();
            if (oldLink == null) return;

            var newLink = new TopicLink
            {
                TopicId = newTopicId,
                Description = oldLink.Description,
                Url = oldLink.Url
            };

            await _uow.TopicLinks.AddAsync(newLink);
        }

        private async Task CloneTopicFileAsync(Guid oldTopicId, Guid newTopicId, CancellationToken ct)
        {
            var oldTopicFile = (await _uow.TopicFiles.FindAsync(f => f.TopicId == oldTopicId, ct)).FirstOrDefault();
            if (oldTopicFile == null) return;

            var newTopicFile = new TopicFile
            {
                TopicId = newTopicId,
                Description = oldTopicFile.Description,
                File = oldTopicFile.File == null ? null : new CloudinaryFile
                {
                    Id = Guid.NewGuid(),
                    Name = oldTopicFile.File.Name,
                    DisplayUrl = oldTopicFile.File.DisplayUrl,
                    DownloadUrl = oldTopicFile.File.DownloadUrl,
                    AssignmentResponseId = null,
                    TopicAssignmentId = null,
                    TopicFileId = newTopicId
                }
            };

            await _uow.TopicFiles.AddAsync(newTopicFile);
        }

        private async Task CloneQuestionBankAsync(
            string oldCourseId,
            string newCourseId,
            Dictionary<Guid, Guid> questionIdMap,
            Guid userId,
            CancellationToken ct)
        {
            var oldQuestions = await _uow.Questions.GetAllByCourseIdAsync(oldCourseId, ct);
            if (oldQuestions == null || !oldQuestions.Any()) return;

            var utcNow = DateTime.UtcNow;

            foreach (var oldQ in oldQuestions)
            {
                var newQuestionId = Guid.NewGuid();
                questionIdMap[oldQ.Id] = newQuestionId;

                var newQuestion = new Question
                {
                    Id = newQuestionId,
                    CreatedAt = utcNow,
                    UpdatedAt = null,
                    DeletedAt = null,
                    QuestionName = oldQ.QuestionName,
                    QuestionText = oldQ.QuestionText,
                    Status = oldQ.Status,
                    Type = oldQ.Type,
                    DefaultMark = oldQ.DefaultMark,
                    Usage = 0,
                    FeedbackOfTrue = oldQ.FeedbackOfTrue,
                    FeedbackOfFalse = oldQ.FeedbackOfFalse,
                    CorrectAnswer = oldQ.CorrectAnswer,
                    Multiple = oldQ.Multiple,
                    CreatedById = userId,
                    ModifiedById = null,
                    CourseId = newCourseId,
                    Choices = new List<QuestionChoice>()
                };

                foreach (var oldChoice in oldQ.Choices ?? new List<QuestionChoice>())
                {
                    newQuestion.Choices.Add(new QuestionChoice
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = newQuestionId,
                        Text = oldChoice.Text,
                        GradePercent = oldChoice.GradePercent,
                        Feedback = oldChoice.Feedback
                    });
                }

                await _uow.Questions.AddAsync(newQuestion);
            }
        }
    } 
}
