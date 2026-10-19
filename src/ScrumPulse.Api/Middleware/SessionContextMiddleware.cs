namespace ScrumPulse.Api.Middleware;

using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.Common.Security;

/// <summary>
/// Projects the validated session token onto <see cref="ITenantContext"/> so the data layer can
/// scope queries and stamp audit fields. Runs after UseAuthentication. Never read X-Team-Id or
/// any other header here: that shipped once, and any visitor could name another squad and read
/// or write its data under a colleague's name.
/// </summary>
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
            // Null makes the tenant filter match nothing, so this reads an empty dataset.
            tenantContext.CurrentTeamId = null;
            tenantContext.CurrentUser = null;
        }

        await next(context);
    }
}
