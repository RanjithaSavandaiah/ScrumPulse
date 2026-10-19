namespace ScrumPulse.Infrastructure.Security;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.Common.Interfaces;

/// <summary>
/// Protects board access tokens using ASP.NET Core Data Protection. The key ring lives on the
/// local file system, so a redeploy loses it and stored tokens stop decrypting; that is why
/// <see cref="Unprotect"/> returns null instead of throwing and the squad is asked to reconnect.
/// </summary>
public sealed class DataProtectionCredentialProtector : ICredentialProtector
{
    // Changing this invalidates every stored credential.
    private const string ProtectionPurpose = "ScrumPulse.BoardConnection.AccessToken.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionCredentialProtector> _logger;

    public DataProtectionCredentialProtector(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DataProtectionCredentialProtector> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Protect(string plainTextCredential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextCredential);

        return _protector.Protect(plainTextCredential);
    }

    /// <inheritdoc />
    public string? Unprotect(string encryptedCredential)
    {
        if (string.IsNullOrWhiteSpace(encryptedCredential))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(encryptedCredential);
        }
        catch (Exception exception)
        {
            // Message omitted: it can echo fragments of the payload.
            _logger.LogWarning(
                "A stored board credential could not be decrypted ({ExceptionType}). " +
                "The squad will need to reconnect its board.",
                exception.GetType().Name);

            return null;
        }
    }
}
