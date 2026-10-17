namespace ScrumPulse.Tests.Filters;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using ScrumPulse.Api.Filters;
using ScrumPulse.Application.Common.Security;
using ScrumPulse.Domain.Enums;
using Xunit;

/// <summary>
/// Covers the privileged-action boundary.
/// </summary>
/// <remarks>
/// The previous version of this suite asserted that a request with no role header and a
/// request with an unparseable role were both allowed through. That was an accurate
/// description of the filter at the time, and it was precisely the vulnerability: sending no
/// headers at all passed every check it guarded. Those two cases now assert the opposite.
/// </remarks>
public class RequireScrumMasterAttributeTests
{
    private static AuthorizationFilterContext CreateFilterContext(RoleType? role)
    {
        var httpContext = new DefaultHttpContext();

        if (role is { } grantedRole)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ScrumPulseClaimTypes.TeamId, Guid.NewGuid().ToString()),
                    new Claim(ScrumPulseClaimTypes.TeamRole, grantedRole.ToString()),
                    new Claim(ScrumPulseClaimTypes.DisplayName, "Test Member")
                ],
                authenticationType: "TestSession"));
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Theory]
    [InlineData(RoleType.ScrumMaster)]
    [InlineData(RoleType.Cdl)]
    [InlineData(RoleType.AgileCoach)]
    public void OnAuthorization_AllowsPrivilegedRoles(RoleType role)
    {
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext(role);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(RoleType.Developer)]
    [InlineData(RoleType.QaEngineer)]
    [InlineData(RoleType.ProductOwner)]
    [InlineData(RoleType.ClientStakeholder)]
    public void OnAuthorization_ForbidsUnprivilegedRoles(RoleType role)
    {
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext(role);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_RejectsAnonymousCaller()
    {
        // The regression that mattered: a bare request carrying no session used to pass
        // every endpoint this filter guarded.
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext(role: null);

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_IgnoresSpoofedRoleHeader()
    {
        // Authorisation reads the signed session, not the request. Setting the old header by
        // hand gains the caller nothing.
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext(RoleType.Developer);
        context.HttpContext.Request.Headers["X-User-Role"] = "ScrumMaster";

        filter.OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_TreatsUnknownRoleAsUnprivileged()
    {
        // A token carrying a role this build does not recognise, for instance after a rename,
        // degrades to Developer rather than being treated as privileged.
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ScrumPulseClaimTypes.TeamId, Guid.NewGuid().ToString()),
                    new Claim(ScrumPulseClaimTypes.TeamRole, "SupremeCommander")
                ],
                authenticationType: "TestSession"))
        };

        var context = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);

        new RequireScrumMasterAttribute().OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_RejectsSessionWithoutTeamClaim()
    {
        // An authenticated principal carrying no team is not a usable squad session.
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ScrumPulseClaimTypes.TeamRole, nameof(RoleType.ScrumMaster))],
                authenticationType: "TestSession"))
        };

        var context = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);

        new RequireScrumMasterAttribute().OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }
}
