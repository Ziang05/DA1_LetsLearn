using LetsLearn.Core.Interfaces;
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
        private readonly ILogger<DeadlineReminderBackgroundService> _logger;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _reminderWindow;

        public DeadlineReminderBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DeadlineReminderBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
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
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var now = DateTime.UtcNow;
            var windowEnd = now.Add(_reminderWindow);

            _logger.LogInformation("[DeadlineReminder] Scanning for deadlines between {Now} and {WindowEnd}.", now, windowEnd);

            // Track sent reminders to avoid duplicate emails
            var sentKey = $"deadline_reminders_sent_{now:yyyyMMddHHmm}";
            var sentCount = 0;

            // ── 1. Assignment deadlines ────────────────────────────────────
            var assignmentDeadlines = await unitOfWork.TopicAssignments.FindAsync(
                a => a.Close != null
                     && a.Close > now
                     && a.Close <= windowEnd,
                ct);

            foreach (var assignment in assignmentDeadlines)
            {
                sentCount += await SendReminderForTopicAsync(
                    unitOfWork, emailService, assignment.TopicId,
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
                sentCount += await SendReminderForTopicAsync(
                    unitOfWork, emailService, quiz.TopicId,
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
                sentCount += await SendMeetingReminderAsync(
                    unitOfWork, emailService, meeting.TopicId,
                    meeting.Open!.Value, meeting.MeetingLink, ct);
            }

            if (sentCount > 0)
            {
                _logger.LogInformation("[DeadlineReminder] Sent {Count} reminder email(s).", sentCount);
            }
        }

        private async Task<int> SendReminderForTopicAsync(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
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

                    try
                    {
                        await emailService.SendDeadlineReminderAsync(
                            student.Email,
                            student.Username ?? "Student",
                            course.Title ?? "Your course",
                            topic.Title ?? $"New {topicType}",
                            topicType,
                            deadline,
                            meetingLink: null);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[DeadlineReminder] Failed to send email to {Email}.", student.Email);
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DeadlineReminder] Error sending reminder for topic {TopicId}.", topicId);
                return 0;
            }
        }

        private async Task<int> SendMeetingReminderAsync(
            IUnitOfWork unitOfWork,
            IEmailService emailService,
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

                    try
                    {
                        await emailService.SendDeadlineReminderAsync(
                            student.Email,
                            student.Username ?? "Student",
                            course.Title ?? "Your course",
                            topic.Title ?? "Meeting",
                            "meeting",
                            startTime,
                            meetingLink);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[DeadlineReminder] Failed to send meeting reminder to {Email}.", student.Email);
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DeadlineReminder] Error sending meeting reminder for topic {TopicId}.", topicId);
                return 0;
            }
        }
    }
}
