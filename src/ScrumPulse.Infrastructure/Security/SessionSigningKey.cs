namespace ScrumPulse.Infrastructure.Security;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Supplies the symmetric key used to sign and validate session tokens. Read from the
/// SESSION_SIGNING_KEY environment variable so it never lands in this public repository.
/// </summary>
public sealed class SessionSigningKey
{
    private const int MinimumKeyLengthBytes = 32;

    private const string EnvironmentVariableName = "SESSION_SIGNING_KEY";

    public SymmetricSecurityKey Key { get; }

    // Ephemeral keys mean sessions die on restart.
    public bool IsEphemeral { get; }

    private SessionSigningKey(SymmetricSecurityKey key, bool isEphemeral)
    {
        Key = key;
        IsEphemeral = isEphemeral;
    }

    // Throws outside development rather than generating a key: a generated one would invalidate
    // every session on restart, and across instances leave one node rejecting another's tokens.
    public static SessionSigningKey Resolve(IConfiguration configuration, bool isDevelopment)
    {
        var configuredKey = Environment.GetEnvironmentVariable(EnvironmentVariableName)
            ?? configuration["Auth:SessionSigningKey"];

        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            var keyBytes = Encoding.UTF8.GetBytes(configuredKey);
            if (keyBytes.Length < MinimumKeyLengthBytes)
            {
                throw new InvalidOperationException(
                    $"{EnvironmentVariableName} must be at least {MinimumKeyLengthBytes} characters. " +
                    $"The configured value is {keyBytes.Length}. Generate one with: openssl rand -base64 48");
            }

            return new SessionSigningKey(new SymmetricSecurityKey(keyBytes), isEphemeral: false);
        }

        if (!isDevelopment)
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} is not configured. Set it to a random value of at least " +
                $"{MinimumKeyLengthBytes} characters before starting in a non-development environment. " +
                "Generate one with: openssl rand -base64 48");
        }

        return new SessionSigningKey(
            new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(MinimumKeyLengthBytes)),
            isEphemeral: true);
    }
}
