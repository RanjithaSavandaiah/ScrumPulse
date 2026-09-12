namespace ScrumPulse.Application.Services;

public record ExportFileResult(byte[] Content, string ContentType, string FileName);

public interface IExportService
{
    Task<ExportFileResult?> ExportSprintCsvAsync(Guid sprintId, CancellationToken ct = default);
    Task<ExportFileResult> ExportEnterpriseJsonAsync(CancellationToken ct = default);
}
