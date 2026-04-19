using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LetsLearn.Core.Interfaces;

namespace LetsLearn.Infrastructure.Redis
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDistributedCache? _cache;

        public RedisCacheService(IDistributedCache? cache)
        {
            _cache = cache;
        }
        public T? Get<T>(string key)
        {
            var data= _cache?.GetString(key);

            if (data is null) return default;

            return JsonSerializer.Deserialize<T>(data);
        }
        public async Task<T?> GetAsync<T>(string key, int opTimeoutMs = 150, CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(opTimeoutMs));

            string? json = null;
            if (_cache != null)
            {
                json = await _cache.GetStringAsync(key, cts.Token);
            }
            if (json is null) return default;
            return JsonSerializer.Deserialize<T>(json);
        }

        public void Remove(string key)
        {
            var data= _cache?.GetString(key);
            if (data is not null)  _cache?.Remove(key);
        }

        public void Set<T>(string key, T value)
        {
            var options = new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            };

            _cache?.SetString(key, JsonSerializer.Serialize(value), options);
        }
    }
}
