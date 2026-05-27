using System.Threading.Channels;
using LetsLearn.UseCases.ServiceInterfaces;

namespace LetsLearn.UseCases.Services.AiQuestionGeneration
{
    public class AiQuestionGenerationJobQueue : IAiQuestionGenerationJobQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        public async ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default)
        {
            await _channel.Writer.WriteAsync(jobId, ct);
        }

        public async ValueTask<Guid> DequeueAsync(CancellationToken ct = default)
        {
            return await _channel.Reader.ReadAsync(ct);
        }
    }
}
