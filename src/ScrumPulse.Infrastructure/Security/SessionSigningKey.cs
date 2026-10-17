namespace ScrumPulse.Infrastructure.Security;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Supplies the symmetric key used to sign and validate session tokens.
/// </summary>
/// <remarks>
/// <para>
/// The key is read from the SESSION_SIGNING_KEY environment variable so it never appears
/// in source control. The repository is public; a committed key would let anyone mint a
/// token for any team and role.
/// </para>
/// <para>
/// Outside development a missing or too-short key is a startup failure rather than a
/// warning. Falling back to a generated key in production would silently invalidate every
/// session on restart and, on a multi-instance deployment, let tokens signed by one
/// instance be rejected by another.
/// </para>
/// </remarks>
public sealed class SessionSigningKey
{
    /// <summary>
    /// Shortest key accepted. HMAC-SHA256 keys shorter than the 256-bit hash output
    /// weaken the signature, and <see cref="SymmetricSecurityKey"/> rejects them outright.
    /// </summary>
    private const int MinimumKeyLengthBytes = 32;

    private const string EnvironmentVariableName = "SESSION_SIGNING_KEY";

    public SymmetricSecurityKey Key { get; }

    /// <summary>
    /// True when no key was configured and an ephemeral one was generated for local use.
    /// Sessions do not survive a restart in this mode.
    /// </summary>
    public bool IsEphemeral { get; }

    private SessionSigningKey(SymmetricSecurityKey key, bool isEphemeral)
    {
        Key = key;
        IsEphemeral = isEphemeral;
    }

    /// <summary>
    /// Resolves the signing key, or throws when running outside development without one.
    /// </summary>
    /// <param name="configuration">Configuration consulted after the environment variable.</param>
    /// <param name="isDevelopment">When true, allows an ephemeral key so the app runs with no setup.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown outside development when the key is absent or shorter than 32 bytes.
    /// </exception>
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
