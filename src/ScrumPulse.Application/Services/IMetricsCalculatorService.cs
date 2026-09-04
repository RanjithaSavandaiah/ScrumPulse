namespace ScrumPulse.Application.Services;
using ScrumPulse.Application.DTOs;

public interface IMetricsCalculatorService
{
    Task<SprintCapacityDto> CalculateSprintCapacityAsync(Guid sprintId, CancellationToken ct = default);
    Task<ExecutiveReportDto> GenerateExecutiveReportAsync(Guid sprintId, CancellationToken ct = default);
    Task<SprintVelocityTrendDto> GetVelocityTrendAsync(int count = 6, CancellationToken ct = default);
    Task<SprintHealthDto> CalculateSprintHealthAsync(Guid sprintId, CancellationToken ct = default);
    Task<SprintComparisonDto> CompareSprintsAsync(Guid sprintAId, Guid sprintBId, CancellationToken ct = default);
}
