namespace ScrumPulse.Application.Services;

using ScrumPulse.Application.DTOs;

// Both import routes live here: they share everything past "we have a list of items", and two
// copies of those rules would drift.
public interface IBoardConnectionService
{
    Task<BoardConnectionDto?> GetConnectionAsync(CancellationToken ct = default);

    /// <summary>One connection per squad, so a second save updates rather than adding.</summary>
    Task<BoardConnectionDto> SaveConnectionAsync(SaveBoardConnectionRequest request, CancellationToken ct = default);

    /// <returns>False when there was nothing to remove.</returns>
    Task<bool> DeleteConnectionAsync(CancellationToken ct = default);

    Task<BoardConnectionTestResult> TestConnectionAsync(CancellationToken ct = default);

    Task<BoardSyncResultDto> SyncAsync(bool fullResync = false, CancellationToken ct = default);

    // For clients who will not issue a token. With nothing stored, creates a credential-less
    // connection to hold the state mapping and content policy.
    Task<BoardSyncResultDto> ImportFromFileAsync(string csvContent, CancellationToken ct = default);
}
