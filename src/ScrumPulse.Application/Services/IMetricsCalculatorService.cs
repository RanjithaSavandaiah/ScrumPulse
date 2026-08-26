namespace ScrumPulse.Application.Services;
using ScrumPulse.Application.DTOs;

public interface IMetricsCalculatorService
{
    Task<SprintCapacityDto> CalculateSprintCapacityAsync(Guid sprintId, CancellationToken ct = default);
    Task<ExecutiveReportDto> GenerateExecutiveReportAsync(Guid sprintId, CancellationToken ct = default);
}
