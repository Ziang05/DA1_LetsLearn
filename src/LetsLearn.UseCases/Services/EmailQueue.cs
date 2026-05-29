using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;

namespace LetsLearn.UseCases.Services
{
    public class EmailQueue : IEmailQueue
    {
        private readonly Channel<EmailJob> _channel = Channel.CreateUnbounded<EmailJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        public async ValueTask QueueEmailAsync(EmailJob job, CancellationToken ct = default)
        {
            await _channel.Writer.WriteAsync(job, ct);
        }

        public async ValueTask<EmailJob> DequeueEmailAsync(CancellationToken ct = default)
        {
            return await _channel.Reader.ReadAsync(ct);
        }
    }
}
