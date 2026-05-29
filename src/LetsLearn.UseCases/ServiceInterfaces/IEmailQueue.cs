using System.Threading;
using System.Threading.Tasks;
using LetsLearn.UseCases.DTOs;

namespace LetsLearn.UseCases.ServiceInterfaces
{
    public interface IEmailQueue
    {
        ValueTask QueueEmailAsync(EmailJob job, CancellationToken ct = default);
        ValueTask<EmailJob> DequeueEmailAsync(CancellationToken ct = default);
    }
}
