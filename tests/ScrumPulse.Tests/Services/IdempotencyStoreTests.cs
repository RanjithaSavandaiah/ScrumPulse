namespace ScrumPulse.Tests.Services;

using ScrumPulse.Infrastructure.Services;
using Xunit;

public class IdempotencyStoreTests
{
    [Fact]
    public async Task SaveAndGet_ReturnsCachedResponse()
    {
        var store = new MemoryIdempotencyStore();
        var key = "test-key-1";
        var response = new { Name = "Test", Value = 42 };

        await store.SaveResponseAsync(key, response);
        var cached = await store.GetResponseAsync<object>(key);

        Assert.NotNull(cached);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenKeyNotFound()
    {
        var store = new MemoryIdempotencyStore();

        var cached = await store.GetResponseAsync<object>("nonexistent-key");

        Assert.Null(cached);
    }

    [Fact]
    public async Task SaveWithTtl_ExpiresAfterDuration()
    {
        var store = new MemoryIdempotencyStore();
        var key = "ttl-key";

        // Save with very short TTL
        await store.SaveResponseAsync(key, "temporary-value", TimeSpan.FromMilliseconds(50));

        // Immediately available
        var immediate = await store.GetResponseAsync<string>(key);
        Assert.NotNull(immediate);

        // Wait for expiry
        await Task.Delay(100);

        var expired = await store.GetResponseAsync<string>(key);
        Assert.Null(expired);
    }

    [Fact]
    public async Task Save_OverwritesExistingKey()
    {
        var store = new MemoryIdempotencyStore();
        var key = "overwrite-key";

        await store.SaveResponseAsync(key, "first");
        await store.SaveResponseAsync(key, "second");

        var result = await store.GetResponseAsync<string>(key);
        Assert.Equal("second", result);
    }

    [Fact]
    public async Task MultipleKeys_AreIndependent()
    {
        var store = new MemoryIdempotencyStore();

        await store.SaveResponseAsync("key-a", "value-a");
        await store.SaveResponseAsync("key-b", "value-b");

        Assert.Equal("value-a", await store.GetResponseAsync<string>("key-a"));
        Assert.Equal("value-b", await store.GetResponseAsync<string>("key-b"));
    }
}
