namespace ScrumPulse.Infrastructure.Services;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.Common.Interfaces;

/// <summary>
/// In-memory idempotency store with automatic background cleanup of expired entries
/// and maximum capacity enforcement (LRU eviction) to prevent unbounded memory growth.
/// </summary>
public sealed class MemoryIdempotencyStore : IIdempotencyStore, IDisposable
{
    private record CacheItem(string SerializedData, DateTime ExpiryUtc);

    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();
    private const int MaxCapacity = 10_000;

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
        // Enforce max capacity with simple eviction of expired entries first
        if (_cache.Count >= MaxCapacity)
        {
            EvictExpiredEntries();
        }

        // If still over capacity after eviction, remove oldest entries
        if (_cache.Count >= MaxCapacity)
        {
            var oldestKeys = _cache
                .OrderBy(kvp => kvp.Value.ExpiryUtc)
                .Take(_cache.Count - MaxCapacity + 1)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var oldKey in oldestKeys)
            {
                _cache.TryRemove(oldKey, out _);
            }
        }

        var duration = expiry ?? TimeSpan.FromHours(1);
        var json = JsonSerializer.Serialize(response);
        _cache[key] = new CacheItem(json, DateTime.UtcNow.Add(duration));
        return Task.CompletedTask;
    }

    /// <summary>Removes all entries whose expiry has passed.</summary>
    internal int EvictExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiryUtc <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
        return expiredKeys.Count;
    }

    /// <summary>Current number of entries in the cache (for diagnostics).</summary>
    public int Count => _cache.Count;

    public void Dispose()
    {
        _cache.Clear();
    }
}

/// <summary>
/// Background hosted service that periodically evicts expired entries
/// from the MemoryIdempotencyStore to prevent unbounded memory growth.
/// </summary>
public sealed class IdempotencyCleanupService : BackgroundService
{
    private readonly MemoryIdempotencyStore _store;
    private readonly ILogger<IdempotencyCleanupService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public IdempotencyCleanupService(IIdempotencyStore store, ILogger<IdempotencyCleanupService> logger)
    {
        _store = (MemoryIdempotencyStore)store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IdempotencyCleanupService started (interval: {Interval})", CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
                var evicted = _store.EvictExpiredEntries();
                if (evicted > 0)
                {
                    _logger.LogInformation("IdempotencyCleanupService evicted {Count} expired entries (remaining: {Remaining})",
                        evicted, _store.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IdempotencyCleanupService encountered an error during cleanup");
            }
        }

        _logger.LogInformation("IdempotencyCleanupService stopped");
    }
}
