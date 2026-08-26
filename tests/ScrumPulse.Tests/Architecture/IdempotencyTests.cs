namespace ScrumPulse.Tests.Architecture;

using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class IdempotencyTests
{
    [Fact]
    public async Task MemoryIdempotencyStore_CachesAndReplaysResponses()
    {
        var store = new MemoryIdempotencyStore();
        var key = "req-unique-key-123";
        var originalDto = new BlockerDto(
            Guid.NewGuid(), "DB Slowdown", "Desc", BlockerCategory.EnvironmentAccess, 4,
            null, null, Guid.NewGuid(), "Alice", null,
            DateTime.UtcNow, null, null, false, 0, false
        );

        Assert.False(await store.ExistsAsync(key));

        await store.SaveResponseAsync(key, originalDto, TimeSpan.FromMinutes(10));

        Assert.True(await store.ExistsAsync(key));
        var cached = await store.GetResponseAsync<BlockerDto>(key);

        Assert.NotNull(cached);
        Assert.Equal(originalDto.Id, cached.Id);
        Assert.Equal("DB Slowdown", cached.Title);
    }
}
