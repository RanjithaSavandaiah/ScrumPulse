namespace ScrumPulse.Application.Services;
using ScrumPulse.Application.DTOs;

/// <summary>
/// Computes aggregated team performance metrics across sprints
/// for client facing growth reporting in service based organizations.
/// </summary>
public interface ITeamPerformanceService
{
    Task<TeamPerformanceSummaryDto> GetPerformanceSummaryAsync(int sprintCount = 6, CancellationToken ct = default);
    Task<IReadOnlyList<TeamHighlightDto>> GetHighlightsAsync(int sprintCount = 6, CancellationToken ct = default);
    Task<IReadOnlyList<SprintGrowthSnapshotDto>> GetGrowthTrendAsync(int sprintCount = 8, CancellationToken ct = default);
}
