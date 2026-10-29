using System.Threading.Channels;
using LetsLearn.UseCases.ServiceInterfaces;

namespace LetsLearn.API.BackgroundServices
{
    public sealed class AiAssignmentGradingJobQueue : IAiAssignmentGradingJobQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default) =>
            _channel.Writer.WriteAsync(jobId, ct);

        public ValueTask<Guid> DequeueAsync(CancellationToken ct = default) =>
            _channel.Reader.ReadAsync(ct);
    }
}
