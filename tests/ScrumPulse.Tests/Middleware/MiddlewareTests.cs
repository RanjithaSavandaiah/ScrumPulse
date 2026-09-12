namespace ScrumPulse.Tests.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.Api.Middleware;
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

public class TenantMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ResolvesTeamIdFromHeader()
    {
        var teamId = Guid.NewGuid();
        var tenantContext = new ScrumPulse.Infrastructure.Services.TenantContext();
        var middleware = new TenantMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Team-Id"] = teamId.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal(teamId, tenantContext.CurrentTeamId);
    }

    [Fact]
    public async Task InvokeAsync_ResolvesTeamIdFromQueryString()
    {
        var teamId = Guid.NewGuid();
        var tenantContext = new ScrumPulse.Infrastructure.Services.TenantContext();
        var middleware = new TenantMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?teamId={teamId}");

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal(teamId, tenantContext.CurrentTeamId);
    }

    [Fact]
    public async Task InvokeAsync_ResolvesUserNameFromHeader()
    {
        var tenantContext = new ScrumPulse.Infrastructure.Services.TenantContext();
        var middleware = new TenantMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Name"] = "Ranjitha";

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal("Ranjitha", tenantContext.CurrentUser);
    }

    [Fact]
    public async Task InvokeAsync_DefaultsToDevloper_WhenNoUserContext()
    {
        var tenantContext = new ScrumPulse.Infrastructure.Services.TenantContext();
        var middleware = new TenantMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal("Developer", tenantContext.CurrentUser);
        Assert.Null(tenantContext.CurrentTeamId);
    }

    [Fact]
    public async Task InvokeAsync_IgnoresEmptyGuidInHeader()
    {
        var tenantContext = new ScrumPulse.Infrastructure.Services.TenantContext();
        var middleware = new TenantMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Team-Id"] = Guid.Empty.ToString();

        await middleware.InvokeAsync(context, tenantContext);

        Assert.Null(tenantContext.CurrentTeamId);
    }
}
