namespace ScrumPulse.Tests.Controllers;

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VerifyPin_WithNullOrWhitespacePin_ReturnsBadRequest(string? pin)
    {
        var config = new ConfigurationBuilder().Build();
        var controller = new AuthController(config, _testEnvironment, NullLogger<AuthController>.Instance);

        var result = controller.VerifyPin(new VerifyPinRequest(pin));

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(badRequestResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public void VerifyPin_InDevelopment_DefaultFallback_Accepts1234()
    {
        var config = new ConfigurationBuilder().Build();
        var controller = new AuthController(config, _testEnvironment, NullLogger<AuthController>.Instance);

        var result = controller.VerifyPin(new VerifyPinRequest("1234"));

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public void VerifyPin_InDevelopment_DefaultFallback_RejectsWrongPin()
    {
        var config = new ConfigurationBuilder().Build();
        var controller = new AuthController(config, _testEnvironment, NullLogger<AuthController>.Instance);

        var result = controller.VerifyPin(new VerifyPinRequest("9999"));

        var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(unauthResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public void VerifyPin_WithCustomConfiguredPin_AcceptsMatchingPin()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Auth:ScrumMasterPin", "8821" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var controller = new AuthController(config, _testEnvironment, NullLogger<AuthController>.Instance);

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
        var controller = new AuthController(config, _testEnvironment, NullLogger<AuthController>.Instance);

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
        var controller = new AuthController(config, _testEnvironment, NullLogger<AuthController>.Instance);

        var result = controller.VerifyPin(new VerifyPinRequest("1234"));

        var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<VerifyPinResponse>(unauthResult.Value);
        Assert.False(response.Success);
    }
}
