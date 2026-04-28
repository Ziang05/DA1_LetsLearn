using System.Threading.Tasks;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
        Task SendPasswordResetAsync(string toEmail, string username, string newPassword);
        Task SendNewTopicNotificationAsync(string toEmail, string studentName, string courseTitle, string topicTitle, string topicType, DateTime? openDate, DateTime? closeDate);
        Task SendDeadlineReminderAsync(string toEmail, string studentName, string courseTitle, string topicTitle, string topicType, DateTime deadline, string? meetingLink);
    }
}
