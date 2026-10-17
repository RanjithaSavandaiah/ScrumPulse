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
            tenantContext.CurrentTeamId = activeSession.TeamId;
            tenantContext.CurrentUser = activeSession.DisplayName;
        }
        else
        {
            // Leaving these null makes the tenant query filter match nothing, so an
            // unauthenticated request reads an empty dataset rather than everyone's.
            tenantContext.CurrentTeamId = null;
            tenantContext.CurrentUser = null;
        }

        await next(context);
    }
}
