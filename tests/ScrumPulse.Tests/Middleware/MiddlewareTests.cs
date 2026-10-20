namespace ScrumPulse.Tests.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using ScrumPulse.Api.Middleware;
using ScrumPulse.Application.Common.Security;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsAllOwaspSecurityHeaders()
    {
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        var headers = context.Response.Headers;
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("1; mode=block", headers["X-XSS-Protection"]);
        Assert.Contains("max-age=31536000", headers["Strict-Transport-Security"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
        Assert.Contains("default-src 'self'", headers["Content-Security-Policy"].ToString());
        Assert.Contains("camera=()", headers["Permissions-Policy"].ToString());
        Assert.Contains("microphone=(self)", headers["Permissions-Policy"].ToString());
        Assert.Contains("geolocation=()", headers["Permissions-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_SuppressesServerFingerprinting()
    {
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();

        // Pre-set headers that should be removed
        context.Response.Headers["X-Powered-By"] = "ASP.NET";
        context.Response.Headers["Server"] = "Kestrel";

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("X-Powered-By"));
        Assert.False(context.Response.Headers.ContainsKey("Server"));
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.True(nextCalled);
    }
}

/// <summary>
/// Covers tenant and audit identity resolution.
/// </summary>
/// <remarks>
/// The suite this replaces asserted that the team came from an X-Team-Id header, the audit
/// name from X-User-Name, and that an anonymous caller defaulted to the identity "Developer".
/// All three were true, and all three were the bug: any visitor could name any squad and
/// attribute their writes to any colleague. Tenant and identity now come only from a
/// validated session token.
/// </remarks>
public class SessionContextMiddlewareTests
{
    private static ClaimsPrincipal CreateSessionPrincipal(Guid teamId, string displayName) =>
        new(new ClaimsIdentity(
            [
                new Claim(ScrumPulseClaimTypes.TeamId, teamId.ToString()),
                new Claim(ScrumPulseClaimTypes.TeamRole, nameof(RoleType.Developer)),
                new Claim(ScrumPulseClaimTypes.DisplayName, displayName)
            ],
            authenticationType: "TestSession"));

    [Fact]
    public async Task InvokeAsync_ResolvesTenantFromSessionToken()
    {
        var teamId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        var middleware = new SessionContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext { User = CreateSessionPrincipal(teamId, "Ranjitha") };

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal(teamId, tenantContext.CurrentTeamId);
        Assert.Equal("Ranjitha", tenantContext.CurrentUser);
    }

    [Fact]
    public async Task InvokeAsync_IgnoresTeamIdHeader()
    {
        // The old escalation path: naming someone else's squad in a header.
        var sessionTeamId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        var middleware = new SessionContextMiddleware(_ => Task.CompletedTask);

        var context = new DefaultHttpContext { User = CreateSessionPrincipal(sessionTeamId, "Ranjitha") };
        context.Request.Headers["X-Team-Id"] = otherTeamId.ToString();
        context.Request.QueryString = new QueryString($"?teamId={otherTeamId}");

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal(sessionTeamId, tenantContext.CurrentTeamId);
        Assert.NotEqual(otherTeamId, tenantContext.CurrentTeamId);
    }

    [Fact]
    public async Task InvokeAsync_IgnoresUserNameHeader()
    {
        // Audit attribution must not be forgeable, or the trail is worthless.
        var tenantContext = new TenantContext();
        var middleware = new SessionContextMiddleware(_ => Task.CompletedTask);

        var context = new DefaultHttpContext { User = CreateSessionPrincipal(Guid.NewGuid(), "Ranjitha") };
        context.Request.Headers["X-User-Name"] = "Someone Else";

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal("Ranjitha", tenantContext.CurrentUser);
    }

    [Fact]
    public async Task InvokeAsync_LeavesTenantUnsetForAnonymousCaller()
    {
        // A null team makes the DbContext query filter match nothing, so an unauthenticated
        // request reads an empty dataset rather than the first squad it can name.
        var tenantContext = new TenantContext();
        var middleware = new SessionContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(new DefaultHttpContext(), tenantContext);

        Assert.Null(tenantContext.CurrentTeamId);
        Assert.Null(tenantContext.CurrentUser);
    }

    [Fact]
    public async Task InvokeAsync_RejectsSessionCarryingEmptyTeamId()
    {
        var tenantContext = new TenantContext();
        var middleware = new SessionContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext { User = CreateSessionPrincipal(Guid.Empty, "Ranjitha") };

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Null(tenantContext.CurrentTeamId);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var nextCalled = false;
        var middleware = new SessionContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext(), new TenantContext());

        Assert.True(nextCalled);
    }
}
