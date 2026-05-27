using System.Collections.Concurrent;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LetsLearn.UseCases.Services.AiQuestionGeneration
{
    public class AiQuotaService : IAiQuotaService
    {
        private static readonly ConcurrentDictionary<string, CounterBucket> MemoryCounters = new();
        private readonly IConfiguration _configuration;
        private readonly Lazy<Task<IConnectionMultiplexer?>> _redis;
        private readonly ILogger<AiQuotaService> _logger;

        public AiQuotaService(IConfiguration configuration, ILogger<AiQuotaService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _redis = new Lazy<Task<IConnectionMultiplexer?>>(ConnectRedisAsync);
        }

        public async Task CheckAndConsumeAsync(Guid userId, string metric, int amount, int limit, TimeSpan window, CancellationToken ct = default)
        {
            if (limit <= 0 || amount <= 0)
            {
                return;
            }

            var bucketKey = BuildBucketKey(userId, metric, window);
            var value = await TryIncrementRedisAsync(bucketKey, amount, window, ct)
                ?? IncrementMemory(bucketKey, amount, window);

            if (value > limit)
            {
                _logger.LogWarning(
                    "[AI Questions] Quota exceeded. UserId={UserId}, Metric={Metric}, Requested={Requested}, Used={Used}, Limit={Limit}, WindowSeconds={WindowSeconds}",
                    userId,
                    metric,
                    amount,
                    value,
                    limit,
                    window.TotalSeconds);
                throw new AiQuotaExceededException($"AI quota exceeded for {metric}. Limit={limit}, requested={amount}, used={value}.");
            }

            _logger.LogInformation(
                "[AI Questions] Quota consumed. UserId={UserId}, Metric={Metric}, Requested={Requested}, Used={Used}, Limit={Limit}, WindowSeconds={WindowSeconds}",
                userId,
                metric,
                amount,
                value,
                limit,
                window.TotalSeconds);
        }

        private async Task<long?> TryIncrementRedisAsync(string key, int amount, TimeSpan window, CancellationToken ct)
        {
            try
            {
                var connection = await _redis.Value.WaitAsync(ct);
                if (connection == null || !connection.IsConnected)
                {
                    return null;
                }

                var db = connection.GetDatabase();
                var value = await db.StringIncrementAsync(key, amount);
                if (value == amount)
                {
                    await db.KeyExpireAsync(key, window);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[AI Questions] Redis quota increment failed; using in-memory quota fallback. Key={Key}", key);
                return null;
            }
        }

        private static long IncrementMemory(string key, int amount, TimeSpan window)
        {
            var now = DateTimeOffset.UtcNow;
            var bucket = MemoryCounters.AddOrUpdate(
                key,
                _ => new CounterBucket(now.Add(window), amount),
                (_, existing) =>
                {
                    if (existing.ExpiresAt <= now)
                    {
                        existing.ExpiresAt = now.Add(window);
                        existing.Value = 0;
                    }

                    existing.Value += amount;
                    return existing;
                });

            return bucket.Value;
        }

        private async Task<IConnectionMultiplexer?> ConnectRedisAsync()
        {
            var connectionString = _configuration.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogInformation("[AI Questions] Redis connection string is empty; using in-memory AI quota counters.");
                return null;
            }

            try
            {
                var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
                _logger.LogInformation("[AI Questions] Redis AI quota connection established.");
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AI Questions] Redis AI quota connection failed; using in-memory AI quota counters.");
                return null;
            }
        }

        private static string BuildBucketKey(Guid userId, string metric, TimeSpan window)
        {
            var now = DateTimeOffset.UtcNow;
            var bucket = window <= TimeSpan.FromMinutes(1)
                ? now.ToString("yyyyMMddHHmm")
                : now.ToString("yyyyMMdd");

            return $"ai-quota:{metric}:{userId:N}:{bucket}";
        }

        private class CounterBucket
        {
            public CounterBucket(DateTimeOffset expiresAt, long value)
            {
                ExpiresAt = expiresAt;
                Value = value;
            }

            public DateTimeOffset ExpiresAt { get; set; }
            public long Value { get; set; }
        }
    }
}
