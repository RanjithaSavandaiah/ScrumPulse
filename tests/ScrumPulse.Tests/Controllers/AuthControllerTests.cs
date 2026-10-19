namespace ScrumPulse.Tests.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.Api.Controllers;
using ScrumPulse.Api.Security;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.Common.Security;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Security;
using Xunit;

/// <summary>
/// Covers session issuance and PIN elevation.
/// </summary>
/// <remarks>
/// The suite this replaces asserted only that a correct PIN returned <c>Success = true</c>.
/// That was the whole of the old contract, and it was the problem: the server told the
/// browser it had authenticated and then kept no record of it, so every later request had to
/// be trusted on the client's word. These tests assert what the endpoints now produce - a
/// signed cookie the server can verify - and that the two secrets cannot be bypassed.
/// </remarks>
public class AuthControllerTests
{
    private const string CorrectPin = "4821";
    private const string JoinCode = "SQUAD-42";

    /// <summary>
    /// Builds a controller over an in-memory database seeded with one joinable squad.
    /// </summary>
    private static (AuthController Controller, AppDbContext DbContext, Guid TeamId) CreateController(
        string? configuredPin = CorrectPin,
        ClaimsPrincipal? caller = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"auth-tests-{Guid.NewGuid()}")
            .Options;

        var dbContext = new AppDbContext(options);

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "Platform Squad",
            Slug = "platform-squad",
            JoinCode = JoinCode,
            IsActive = true
        };

        dbContext.Teams.Add(team);
        dbContext.SaveChanges();

        var settings = new Dictionary<string, string?>
        {
            ["Auth:ScrumMasterPin"] = configuredPin,
            ["Auth:SessionSigningKey"] = "test-signing-key-that-is-long-enough-to-be-valid"
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var signingKey = SessionSigningKey.Resolve(configuration, isDevelopment: true);

        var controller = new AuthController(
            dbContext,
            new JwtSessionTokenIssuer(signingKey),
            configuration,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = caller ?? new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        return (controller, dbContext, team.Id);
    }

    private static ClaimsPrincipal CreateSession(Guid teamId, RoleType role) =>
        new(new ClaimsIdentity(
            [
                new Claim(ScrumPulseClaimTypes.TeamId, teamId.ToString()),
                new Claim(ScrumPulseClaimTypes.TeamRole, role.ToString()),
                new Claim(ScrumPulseClaimTypes.DisplayName, "Ranjitha")
            ],
            authenticationType: "TestSession"));

    private static bool HasSessionCookie(ControllerBase controller) =>
        controller.Response.Headers.SetCookie
            .Any(header => header is not null && header.Contains("ScrumPulseSession", StringComparison.Ordinal));

    [Fact]
    public async Task Join_WithCorrectJoinCode_IssuesDeveloperSession()
    {
        var (controller, _, teamId) = CreateController();

        var result = await controller.Join(new JoinSquadRequest(JoinCode, "Ranjitha"));

        var response = Assert.IsType<SessionStateResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(response.IsAuthenticated);
        Assert.Equal(teamId, response.TeamId);
        Assert.Equal(nameof(RoleType.Developer), response.Role);
        Assert.True(HasSessionCookie(controller), "Joining a squad must issue a session cookie.");
    }

    [Fact]
    public async Task Join_WithUnknownJoinCode_IsRejectedAndIssuesNoSession()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.Join(new JoinSquadRequest("NOT-A-REAL-CODE", "Mallory"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.False(HasSessionCookie(controller), "A failed join must not issue a session.");
    }

    [Fact]
    public async Task Join_WithInactiveSquad_IsRejected()
    {
        var (controller, dbContext, teamId) = CreateController();

        var team = await dbContext.Teams.FirstAsync(candidate => candidate.Id == teamId);
        team.IsActive = false;
        await dbContext.SaveChangesAsync();

        var result = await controller.Join(new JoinSquadRequest(JoinCode, "Ranjitha"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public void VerifyPin_WithCorrectPin_ElevatesExistingSession()
    {
        var teamId = Guid.NewGuid();
        var (controller, _, _) = CreateController(caller: CreateSession(teamId, RoleType.Developer));

        var result = controller.VerifyPin(new VerifyPinRequest(CorrectPin));

        var response = Assert.IsType<SessionStateResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(nameof(RoleType.ScrumMaster), response.Role);

        // Elevation must stay inside the squad the caller already belongs to.
        Assert.Equal(teamId, response.TeamId);
        Assert.True(HasSessionCookie(controller), "Elevation must re-issue the session cookie.");
    }

    [Fact]
    public void VerifyPin_WithoutAnExistingSession_IsRejected()
    {
        // The PIN alone must not be a way into a squad. Without membership there is no team
        // to elevate within, so knowing the PIN gains nothing.
        var (controller, _, _) = CreateController();

        var result = controller.VerifyPin(new VerifyPinRequest(CorrectPin));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.False(HasSessionCookie(controller));
    }

    [Fact]
    public void VerifyPin_WithIncorrectPin_DoesNotElevate()
    {
        var (controller, _, _) = CreateController(caller: CreateSession(Guid.NewGuid(), RoleType.Developer));

        var result = controller.VerifyPin(new VerifyPinRequest("0000"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.False(HasSessionCookie(controller));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VerifyPin_WithBlankPin_ReturnsValidationProblem(string? pin)
    {
        var (controller, _, _) = CreateController(caller: CreateSession(Guid.NewGuid(), RoleType.Developer));
        controller.ModelState.AddModelError(nameof(VerifyPinRequest.Pin), "Enter the Scrum Master PIN.");

        var result = controller.VerifyPin(new VerifyPinRequest(pin));

        Assert.IsType<ObjectResult>(result.Result);
    }

    [Fact]
    public void VerifyPin_WhenPinNotConfigured_RefusesRatherThanAllowing()
    {
        // A deployment that forgot to set SM_PIN must deny elevation, not grant it.
        var (controller, _, _) = CreateController(
            configuredPin: null,
            caller: CreateSession(Guid.NewGuid(), RoleType.Developer));

        var result = controller.VerifyPin(new VerifyPinRequest("anything"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSession_ForAnonymousCaller_ReportsUnauthenticated()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetSession();

        var response = Assert.IsType<SessionStateResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(response.IsAuthenticated);
        Assert.Null(response.TeamId);
    }

    [Fact]
    public async Task GetSession_ForActiveSession_ReportsTeamAndRole()
    {
        var (controller, _, teamId) = CreateController();
        controller.ControllerContext.HttpContext.User = CreateSession(teamId, RoleType.ScrumMaster);

        var result = await controller.GetSession();

        var response = Assert.IsType<SessionStateResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(response.IsAuthenticated);
        Assert.Equal(teamId, response.TeamId);
        Assert.Equal("Platform Squad", response.TeamName);
        Assert.Equal(nameof(RoleType.ScrumMaster), response.Role);
    }

    [Fact]
    public async Task Join_IssuesCookieThatScriptCannotReadAndOtherSitesCannotSend()
    {
        var (controller, _, _) = CreateController();

        await controller.Join(new JoinSquadRequest(JoinCode, "Ranjitha"));

        var cookie = Assert.Single(controller.Response.Headers.SetCookie.ToArray());
        Assert.NotNull(cookie);

        // HttpOnly keeps an XSS bug from exfiltrating a Scrum Master session; SameSite=Strict
        // is what removes the CSRF exposure on the mutating endpoints.
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
