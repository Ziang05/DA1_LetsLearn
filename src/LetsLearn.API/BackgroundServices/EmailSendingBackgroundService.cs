using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LetsLearn.API.BackgroundServices
{
    public class EmailSendingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEmailQueue _queue;
        private readonly ILogger<EmailSendingBackgroundService> _logger;

        public EmailSendingBackgroundService(
            IServiceProvider serviceProvider,
            IEmailQueue queue,
            ILogger<EmailSendingBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[Email Queue] Background service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _queue.DequeueEmailAsync(stoppingToken);
                    await SendEmailWithRetryAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Email Queue] Error processing email from queue.");
                }
            }

            _logger.LogInformation("[Email Queue] Background service stopped.");
        }

        private async Task SendEmailWithRetryAsync(EmailJob job, CancellationToken ct)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("[Email Queue] Attempting to send email type {Type} to {Email} (Attempt {Attempt}/{MaxRetries})", job.Type, job.ToEmail, attempt, maxRetries);

                    switch (job.Type)
                    {
                        case EmailType.DeadlineReminder:
                            await emailService.SendDeadlineReminderAsync(
                                job.ToEmail,
                                job.StudentName ?? "Student",
                                job.CourseTitle ?? "Your course",
                                job.TopicTitle ?? "Activity",
                                job.TopicType ?? "activity",
                                job.Deadline ?? DateTime.UtcNow,
                                job.MeetingLink);
                            break;

                        case EmailType.NewTopicNotification:
                            await emailService.SendNewTopicNotificationAsync(
                                job.ToEmail,
                                job.StudentName ?? "Student",
                                job.CourseTitle ?? "Your course",
                                job.TopicTitle ?? "Activity",
                                job.TopicType ?? "activity",
                                job.OpenDate,
                                job.CloseDate);
                            break;

                        case EmailType.PasswordReset:
                            await emailService.SendPasswordResetAsync(
                                job.ToEmail,
                                job.Username ?? "Student",
                                job.NewPassword ?? "");
                            break;

                        case EmailType.Custom:
                            await emailService.SendAsync(
                                job.ToEmail,
                                job.Subject ?? "",
                                job.Body ?? "");
                            break;
                    }

                    _logger.LogInformation("[Email Queue] Successfully sent email to {Email}.", job.ToEmail);
                    return; // Success!
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Email Queue] Failed sending email to {Email} on attempt {Attempt}.", job.ToEmail, attempt);
                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "[Email Queue] Max retries reached. Email to {Email} failed permanently.", job.ToEmail);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5 * attempt), ct);
                    }
                }
            }
        }
    }
}
