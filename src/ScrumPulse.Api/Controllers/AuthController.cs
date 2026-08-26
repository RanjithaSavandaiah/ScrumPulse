namespace ScrumPulse.Api.Controllers;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

public record VerifyPinRequest(string? Pin);
public record VerifyPinResponse(bool Success, string Message);

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
    public IActionResult VerifyPin([FromBody] VerifyPinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Pin))
        {
            return BadRequest(new VerifyPinResponse(false, "PIN cannot be empty."));
        }

        // Retrieve configured PIN from environment variable or configuration
        var configuredPin = _configuration["Auth:ScrumMasterPin"]
            ?? _configuration["SM_PIN"]
            ?? Environment.GetEnvironmentVariable("SM_PIN");

        if (string.IsNullOrWhiteSpace(configuredPin))
        {
            if (_environment.IsDevelopment())
            {
                configuredPin = "1234";
            }
            else
            {
                _logger.LogWarning("Scrum Master PIN is not configured in production environment.");
                return Unauthorized(new VerifyPinResponse(false, "Scrum Master PIN authentication is unconfigured on server."));
            }
        }

        var inputBytes = Encoding.UTF8.GetBytes(request.Pin.Trim());
        var expectedBytes = Encoding.UTF8.GetBytes(configuredPin.Trim());

        // Constant-time comparison to prevent timing attacks
        var isValid = inputBytes.Length == expectedBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(inputBytes, expectedBytes);

        if (!isValid)
        {
            return Unauthorized(new VerifyPinResponse(false, "Incorrect Security PIN. Scrum Master access denied."));
        }

        return Ok(new VerifyPinResponse(true, "Authentication successful."));
    }
}
