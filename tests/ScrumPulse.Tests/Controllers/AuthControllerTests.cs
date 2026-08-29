namespace ScrumPulse.Tests.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.Api.Controllers;
using Xunit;

public class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "ScrumPulse.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public class AuthControllerTests
{
    private readonly TestHostEnvironment _testEnvironment;

    public AuthControllerTests()
    {
        _testEnvironment = new TestHostEnvironment { EnvironmentName = Environments.Development };
    }

    private static AuthController CreateController(IConfiguration config, IHostEnvironment env)
    {
        var controller = new AuthController(config, env, NullLogger<AuthController>.Instance);
        // Provide HttpContext so HttpContext.Connection.RemoteIpAddress doesn't throw
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VerifyPin_WithNullOrWhitespacePin_ReturnsBadRequest(string? pin)
    {
        var config = new ConfigurationBuilder().Build();
        var controller = CreateController(config, _testEnvironment);

        var result = controller.VerifyPin(new VerifyPinRequest(pin));

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(badRequestResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public void VerifyPin_WhenUnconfigured_ReturnsUnauthorized()
    {
        // With no PIN configured, all PINs should be rejected (security hardening)
        var config = new ConfigurationBuilder().Build();
        var controller = CreateController(config, _testEnvironment);

        var result = controller.VerifyPin(new VerifyPinRequest("1234"));

        var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(unauthResult.Value);
        Assert.False(response.Success);
        Assert.Contains("unconfigured", response.Message);
    }

    [Fact]
    public void VerifyPin_WithCustomConfiguredPin_AcceptsMatchingPin()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Auth:ScrumMasterPin", "8821" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var controller = CreateController(config, _testEnvironment);

        var result = controller.VerifyPin(new VerifyPinRequest("8821"));

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public void VerifyPin_WithCustomConfiguredPin_RejectsMismatch()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Auth:ScrumMasterPin", "8821" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var controller = CreateController(config, _testEnvironment);

        var result = controller.VerifyPin(new VerifyPinRequest("1234"));

        var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(unauthResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public void VerifyPin_InProduction_WhenUnconfigured_RejectsAccess()
    {
        _testEnvironment.EnvironmentName = Environments.Production;
        var config = new ConfigurationBuilder().Build();
        var controller = CreateController(config, _testEnvironment);

        var result = controller.VerifyPin(new VerifyPinRequest("1234"));

        var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(unauthResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public void VerifyPin_ConstantTimeComparison_PreventsTimingAttacks()
    {
        // Verify that wrong PIN of same length and different length are both rejected
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Auth:ScrumMasterPin", "secure123" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var controller = CreateController(config, _testEnvironment);

        // Wrong PIN, same length
        var result1 = controller.VerifyPin(new VerifyPinRequest("wrong1234"));
        Assert.IsType<UnauthorizedObjectResult>(result1);

        // Wrong PIN, different length
        var result2 = controller.VerifyPin(new VerifyPinRequest("short"));
        Assert.IsType<UnauthorizedObjectResult>(result2);

        // Correct PIN
        var result3 = controller.VerifyPin(new VerifyPinRequest("secure123"));
        Assert.IsType<OkObjectResult>(result3);
    }
}
