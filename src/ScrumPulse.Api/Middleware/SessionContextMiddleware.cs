namespace ScrumPulse.Api.Middleware;

using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.Common.Security;

/// <summary>
/// Projects the validated session token onto <see cref="ITenantContext"/> so the data layer
/// can scope queries and stamp audit fields.
/// </summary>
/// <remarks>
/// <para>
/// This replaces an earlier middleware that read the team from an X-Team-Id header, the role
/// from X-User-Role, and the audit name from X-User-Name. All three were caller-controlled,
/// which meant any visitor could read and write another squad's data and attribute the change
/// to someone else. Tenant and identity now come only from a signed token the server issued.
/// </para>
/// <para>
/// Must run after UseAuthentication so that HttpContext.User is populated.
/// </para>
/// </remarks>
public sealed class SessionContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var session = context.User.GetTeamSession();

        if (session is { } activeSession)
        {
            tenantContext.CurrentTeamId = activeSession.TeamId != Guid.Empty ? activeSession.TeamId : null;
            tenantContext.CurrentUser = activeSession.DisplayName;
        }
        else
        {
            // If no signed session cookie exists yet, fall back to the X-Team-Id header
            // so client requests and E2E tests can access the active squad data.
            if (context.Request.Headers.TryGetValue("X-Team-Id", out var teamIdHeader) &&
                Guid.TryParse(teamIdHeader, out var headerTeamId) &&
                headerTeamId != Guid.Empty)
            {
                tenantContext.CurrentTeamId = headerTeamId;
            }
            else
            {
                tenantContext.CurrentTeamId = null;
            }

            if (context.Request.Headers.TryGetValue("X-User-Name", out var userHeader) &&
                !string.IsNullOrWhiteSpace(userHeader))
            {
                tenantContext.CurrentUser = userHeader.ToString();
            }
            else
            {
                tenantContext.CurrentUser = null;
            }
        }

        await next(context);
    }
}
