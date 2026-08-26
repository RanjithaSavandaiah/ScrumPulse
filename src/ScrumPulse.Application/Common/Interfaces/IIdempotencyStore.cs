namespace ScrumPulse.Application.Common.Interfaces;

public interface IIdempotencyStore
{
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<T?> GetResponseAsync<T>(string key, CancellationToken ct = default);
    Task SaveResponseAsync<T>(string key, T response, TimeSpan? expiry = null, CancellationToken ct = default);
}
