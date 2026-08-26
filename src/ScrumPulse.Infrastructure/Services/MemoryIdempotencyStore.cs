namespace ScrumPulse.Infrastructure.Services;

using System.Collections.Concurrent;
using System.Text.Json;
using ScrumPulse.Application.Common.Interfaces;

public class MemoryIdempotencyStore : IIdempotencyStore
{
    private record CacheItem(string SerializedData, DateTime ExpiryUtc);
    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var item))
        {
            if (item.ExpiryUtc > DateTime.UtcNow) return Task.FromResult(true);
            _cache.TryRemove(key, out _);
        }
        return Task.FromResult(false);
    }

    public Task<T?> GetResponseAsync<T>(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var item))
        {
            if (item.ExpiryUtc > DateTime.UtcNow)
            {
                return Task.FromResult(JsonSerializer.Deserialize<T>(item.SerializedData));
            }
            _cache.TryRemove(key, out _);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SaveResponseAsync<T>(string key, T response, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var duration = expiry ?? TimeSpan.FromHours(1);
        var json = JsonSerializer.Serialize(response);
        _cache[key] = new CacheItem(json, DateTime.UtcNow.Add(duration));
        return Task.CompletedTask;
    }
}
