namespace ScrumPulse.Api.Controllers;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

public record VerifyPinRequest(string? Pin);
public record VerifyPinResponse(bool Success, string Message);

/// <summary>Authentication controller with strict rate limiting for brute-force protection.</summary>
[EnableRateLimiting("auth")]
public class AuthController : BaseApiController
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, IHostEnvironment environment, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("verify-pin")]
    [ProducesResponseType(typeof(VerifyPinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(VerifyPinResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(VerifyPinResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult VerifyPin([FromBody] VerifyPinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Pin))
        {
            return BadRequest(new VerifyPinResponse(false, "PIN cannot be empty."));
        }

        // Retrieve configured PIN from environment variable or configuration
        var configuredPin = Environment.GetEnvironmentVariable("SM_PIN")
            ?? _configuration["Auth:ScrumMasterPin"];

        if (string.IsNullOrWhiteSpace(configuredPin))
        {
            _logger.LogWarning("Scrum Master PIN is not configured. Set SM_PIN environment variable.");
            return Unauthorized(new VerifyPinResponse(false, "Scrum Master PIN authentication is unconfigured on server."));
        }

        var inputBytes = Encoding.UTF8.GetBytes(request.Pin.Trim());
        var expectedBytes = Encoding.UTF8.GetBytes(configuredPin.Trim());

        // Constant-time comparison to prevent timing attacks
        var isValid = inputBytes.Length == expectedBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(inputBytes, expectedBytes);

        if (!isValid)
        {
            _logger.LogWarning("Failed PIN verification attempt from {RemoteIp}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new VerifyPinResponse(false, "Incorrect Security PIN. Scrum Master access denied."));
        }

        return Ok(new VerifyPinResponse(true, "Authentication successful."));
    }
}
