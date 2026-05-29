using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LetsLearn.API.BackgroundServices
{
    public class DeadlineReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEmailQueue _emailQueue;
        private readonly ILogger<DeadlineReminderBackgroundService> _logger;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _reminderWindow;

        public DeadlineReminderBackgroundService(
            IServiceProvider serviceProvider,
            IEmailQueue emailQueue,
            ILogger<DeadlineReminderBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _emailQueue = emailQueue;
            _logger = logger;

            var intervalMins = configuration.GetValue<int>("DeadlineReminder:IntervalMinutes", 2);
            var windowMins = configuration.GetValue<int>("DeadlineReminder:ReminderWindowMinutes", 30);

            _interval = TimeSpan.FromMinutes(intervalMins);
            _reminderWindow = TimeSpan.FromMinutes(windowMins);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[DeadlineReminder] Background service started. Checking every {Interval} minutes.", _interval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDeadlineRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DeadlineReminder] Error while processing deadline reminders.");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("[DeadlineReminder] Background service stopped.");
        }

        private async Task ProcessDeadlineRemindersAsync(CancellationToken ct)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var now = DateTime.UtcNow;
            var windowEnd = now.Add(_reminderWindow);

            _logger.LogInformation("[DeadlineReminder] Scanning for deadlines between {Now} and {WindowEnd}.", now, windowEnd);

            var queuedCount = 0;

            // ── 1. Assignment deadlines ────────────────────────────────────
            var assignmentDeadlines = await unitOfWork.TopicAssignments.FindAsync(
                a => a.Close != null
                     && a.Close > now
                     && a.Close <= windowEnd,
                ct);

            foreach (var assignment in assignmentDeadlines)
            {
                queuedCount += await QueueReminderForTopicAsync(
                    unitOfWork, assignment.TopicId,
                    "assignment", assignment.Close!.Value, ct);
            }

            // ── 2. Quiz deadlines ────────────────────────────────────────
            var quizDeadlines = await unitOfWork.TopicQuizzes.FindAsync(
                q => q.Close != null
                     && q.Close > now
                     && q.Close <= windowEnd,
                ct);

            foreach (var quiz in quizDeadlines)
            {
                queuedCount += await QueueReminderForTopicAsync(
                    unitOfWork, quiz.TopicId,
                    "quiz", quiz.Close!.Value, ct);
            }

            // ── 3. Meeting start reminders ───────────────────────────────
            var meetingStarts = await unitOfWork.TopicMeetings.FindAsync(
                m => m.Open != null
                     && m.Open > now
                     && m.Open <= windowEnd,
                ct);

            foreach (var meeting in meetingStarts)
            {
                queuedCount += await QueueMeetingReminderAsync(
                    unitOfWork, meeting.TopicId,
                    meeting.Open!.Value, meeting.MeetingLink, ct);
            }

            if (queuedCount > 0)
            {
                _logger.LogInformation("[DeadlineReminder] Queued {Count} reminder email(s).", queuedCount);
            }
        }

        private async Task<int> QueueReminderForTopicAsync(
            IUnitOfWork unitOfWork,
            Guid topicId,
            string topicType,
            DateTime deadline,
            CancellationToken ct)
        {
            try
            {
                var topic = await unitOfWork.Topics.GetByIdAsync(topicId, ct);
                if (topic == null) return 0;

                var section = await unitOfWork.Sections.GetByIdAsync(topic.SectionId, ct);
                if (section == null) return 0;

                var course = await unitOfWork.Course.GetByIdAsync(section.CourseId, ct);
                if (course == null) return 0;

                var enrollments = await unitOfWork.Enrollments.GetAllByCourseIdAsync(section.CourseId, ct);
                int count = 0;

                foreach (var enrollment in enrollments)
                {
                    var student = await unitOfWork.Users.GetByIdAsync(enrollment.StudentId, ct);
                    if (student == null) continue;

                    var role = student.Role?.ToLower();
                    if (role != "student" && role != "learner") continue;
                    if (string.IsNullOrEmpty(student.Email)) continue;

                    await _emailQueue.QueueEmailAsync(new EmailJob
                    {
                        Type = EmailType.DeadlineReminder,
                        ToEmail = student.Email,
                        StudentName = student.Username ?? "Student",
                        CourseTitle = course.Title ?? "Your course",
                        TopicTitle = topic.Title ?? $"New {topicType}",
                        TopicType = topicType,
                        Deadline = deadline
                    }, ct);
                    count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DeadlineReminder] Error queueing reminder for topic {TopicId}.", topicId);
                return 0;
            }
        }

        private async Task<int> QueueMeetingReminderAsync(
            IUnitOfWork unitOfWork,
            Guid topicId,
            DateTime startTime,
            string? meetingLink,
            CancellationToken ct)
        {
            try
            {
                var topic = await unitOfWork.Topics.GetByIdAsync(topicId, ct);
                if (topic == null) return 0;

                var section = await unitOfWork.Sections.GetByIdAsync(topic.SectionId, ct);
                if (section == null) return 0;

                var course = await unitOfWork.Course.GetByIdAsync(section.CourseId, ct);
                if (course == null) return 0;

                var enrollments = await unitOfWork.Enrollments.GetAllByCourseIdAsync(section.CourseId, ct);
                int count = 0;

                foreach (var enrollment in enrollments)
                {
                    var student = await unitOfWork.Users.GetByIdAsync(enrollment.StudentId, ct);
                    if (student == null) continue;

                    var role = student.Role?.ToLower();
                    if (role != "student" && role != "learner") continue;
                    if (string.IsNullOrEmpty(student.Email)) continue;

                    await _emailQueue.QueueEmailAsync(new EmailJob
                    {
                        Type = EmailType.DeadlineReminder,
                        ToEmail = student.Email,
                        StudentName = student.Username ?? "Student",
                        CourseTitle = course.Title ?? "Your course",
                        TopicTitle = topic.Title ?? "Meeting",
                        TopicType = "meeting",
                        Deadline = startTime,
                        MeetingLink = meetingLink
                    }, ct);
                    count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DeadlineReminder] Error queueing meeting reminder for topic {TopicId}.", topicId);
                return 0;
            }
        }
    }
}
