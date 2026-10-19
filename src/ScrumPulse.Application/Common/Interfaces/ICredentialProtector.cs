namespace ScrumPulse.Application.Common.Interfaces;

/// <summary>
/// Encrypts and decrypts the access tokens used to reach a squad's delivery board.
/// </summary>
/// <remarks>
/// These tokens can read an organisation's backlog, so they must never be stored in a form
/// that a database dump would expose. Behind an interface so the application layer can hold
/// a connection without depending on a particular cryptographic implementation, and so tests
/// can substitute a trivial one.
/// </remarks>
public interface ICredentialProtector
{
    /// <summary>Encrypts a token for storage.</summary>
    string Protect(string plainTextCredential);

    /// <summary>
    /// Decrypts a stored token.
    /// </summary>
    /// <returns>
    /// The token, or null when it cannot be decrypted - which happens if the signing key was
    /// rotated or the row was copied from another environment. Null rather than an exception
    /// so a sync reports "reconnect the board" instead of failing the whole dashboard.
    /// </returns>
    string? Unprotect(string encryptedCredential);
}
