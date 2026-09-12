namespace ScrumPulse.Tests.Filters;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using ScrumPulse.Api.Filters;
using Xunit;

public class RequireScrumMasterAttributeTests
{
    private static ActionExecutingContext CreateFilterContext(string? role = null)
    {
        var httpContext = new DefaultHttpContext();
        if (role != null)
        {
            httpContext.Request.Headers["X-User-Role"] = role;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }

    [Theory]
    [InlineData("ScrumMaster")]
    [InlineData("Cdl")]
    [InlineData("AgileCoach")]
    [InlineData("scrummaster")]   // case insensitive
    [InlineData("agilecoach")]    // case insensitive
    public void OnActionExecuting_AllowsAuthorizedRoles(string role)
    {
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext(role);

        filter.OnActionExecuting(context);

        Assert.Null(context.Result); // No result means the filter allowed through
    }

    [Theory]
    [InlineData("Developer")]
    [InlineData("QaEngineer")]
    [InlineData("ProductOwner")]
    public void OnActionExecuting_BlocksUnauthorizedRoles(string role)
    {
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext(role);

        filter.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void OnActionExecuting_AllowsWhenNoRoleHeader()
    {
        // No X-User-Role header at all should pass through
        // (backwards compatibility — public read endpoints)
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext(role: null);

        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnActionExecuting_AllowsWhenRoleIsUnparseable()
    {
        // Garbage role value that doesn't match any RoleType enum
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext("NotAValidRole");

        filter.OnActionExecuting(context);

        Assert.Null(context.Result); // Unparseable roles pass through (not denied)
    }

    [Fact]
    public void OnActionExecuting_HandlesRoleWithSpaces()
    {
        var filter = new RequireScrumMasterAttribute();
        var context = CreateFilterContext("Scrum Master"); // spaces are stripped

        filter.OnActionExecuting(context);

        Assert.Null(context.Result); // "ScrumMaster" after space removal → allowed
    }
}
