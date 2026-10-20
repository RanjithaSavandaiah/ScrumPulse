namespace ScrumPulse.Api.Security;

using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;
using ScrumPulse.Application.Common.Security;

/// <summary>
/// Output cache policy that keys every cached response by the team of the requesting session.
/// </summary>
/// <remarks>
/// <para>
/// Cache keys previously varied on the <c>X-Team-Id</c> request header. Once tenancy moved
/// into the session cookie nothing sends that header any more, so every squad would have
/// collapsed onto a single cache entry and the first squad to request a report would have
/// served it to the next. Varying on the team claim restores per-squad isolation.
/// </para>
/// <para>
/// Responses for anonymous callers are not cached at all. They contain no squad data, and
/// caching them risks an entry being reused once a session appears.
/// </para>
/// </remarks>
public sealed class TenantOutputCachePolicy : IOutputCachePolicy
{
    /// <summary>Default lifetime, used by the base policy applied to every endpoint.</summary>
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(15);

    private readonly TimeSpan _duration;

    /// <summary>
    /// Creates the policy with the default lifetime.
    /// </summary>
    /// <remarks>
    /// Required by <c>AddPolicy&lt;T&gt;()</c>, which activates the policy type and so needs a
    /// parameterless constructor. Named policies use the overload below instead.
    /// </remarks>
    public TenantOutputCachePolicy() : this(DefaultDuration)
    {
    }

    public TenantOutputCachePolicy(TimeSpan duration) => _duration = duration;

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var session = context.HttpContext.User.GetTeamSession();

        if (session is null || context.HttpContext.Request.Path.StartsWithSegments("/api/teams"))
        {
            context.EnableOutputCaching = false;
            return ValueTask.CompletedTask;
        }

        context.EnableOutputCaching = true;
        context.ResponseExpirationTimeSpan = _duration;

        // Only successful GET and HEAD responses are safe to reuse.
        context.AllowCacheLookup = HttpMethods.IsGet(context.HttpContext.Request.Method)
            || HttpMethods.IsHead(context.HttpContext.Request.Method);
        context.AllowCacheStorage = context.AllowCacheLookup;
        context.AllowLocking = true;

        context.CacheVaryByRules.CacheKeyPrefix = $"team:{session.Value.TeamId}";

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        // Never store a response that carries a Set-Cookie header: it would hand one
        // caller's freshly issued session token to the next caller of the same endpoint.
        if (!StringValues.IsNullOrEmpty(context.HttpContext.Response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK)
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}
