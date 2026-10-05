namespace ScrumPulse.Api.Filters;

using System.Collections.Frozen;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Authorization filter restricting endpoint access to Scrum Master, CDL, and Agile Coach roles.
/// Reads the <c>X-User-Role</c> header and returns 403 Forbidden for unauthorized roles.
/// Apply via <c>[RequireScrumMaster]</c> on controller actions that require elevated privileges.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireScrumMasterAttribute : ActionFilterAttribute
{
    private static readonly FrozenSet<RoleType> AuthorizedRoles =
    new[]
    {
        RoleType.ScrumMaster,
        RoleType.Cdl,
        RoleType.AgileCoach
    }.ToFrozenSet();

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Headers.TryGetValue("X-User-Role", out var roleHeader))
        {
            var rawRole = roleHeader.ToString().Replace(" ", "");
            if (Enum.TryParse<RoleType>(rawRole, ignoreCase: true, out var role) &&
                !AuthorizedRoles.Contains(role))
            {
                context.Result = new ObjectResult(new { error = "Only Scrum Masters can perform this action." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        base.OnActionExecuting(context);
    }
}
