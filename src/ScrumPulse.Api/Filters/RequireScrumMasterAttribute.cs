namespace ScrumPulse.Api.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ScrumPulse.Application.Common.Security;

/// <summary>
/// Restricts an endpoint to sessions holding a privileged role: Scrum Master, CDL, or Agile Coach.
/// </summary>
/// <remarks>
/// <para>
/// The previous implementation read the role from an <c>X-User-Role</c> request header, which
/// the caller controls, and let the request through whenever that header was absent or did not
/// parse. A plain curl with no headers therefore passed every check it guarded.
/// </para>
/// <para>
/// Authorisation now derives from the signed session token and denies by default: no session,
/// or a session without a privileged role, is rejected. That role can only have been written
/// into the token by the server, after the caller supplied the correct squad PIN.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireScrumMasterAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.User.GetTeamSession();

        if (session is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No active squad session",
                Detail = "Join a squad with its join code to continue."
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        if (!session.Value.IsPrivileged)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Scrum Master access required",
                Detail = "Switch to the Scrum Master role and enter the squad PIN to perform this action."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
