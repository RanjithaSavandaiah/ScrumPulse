namespace ScrumPulse.Api.Controllers;

using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScrumPulse.Api.Filters;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Integration;
using ScrumPulse.Application.Services;

/// <summary>
/// Configures the squad's delivery board link and runs imports against it. No endpoint returns
/// the stored credential; the browser only learns whether one exists.
/// </summary>
[Route("api/board-connections")]
[RequireScrumMaster]
public sealed class BoardConnectionsController(
    IBoardConnectionService boardConnectionService,
    ILogger<BoardConnectionsController> logger) : BaseApiController
{
    // Also applied as endpoint metadata so Kestrel rejects an oversized body before buffering
    // it into a free instance's memory.
    private const int MaxImportUploadBytes = 8 * 1024 * 1024;

    /// <summary>Returns the squad's connection, or 204 when none is configured.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BoardConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<BoardConnectionDto>> GetConnection(CancellationToken ct = default)
    {
        var connection = await boardConnectionService.GetConnectionAsync(ct);

        return connection is null ? NoContent() : Ok(connection);
    }

    [HttpGet("state-mapping-defaults")]
    [ProducesResponseType(typeof(BoardStateMappingDefaultsDto), StatusCodes.Status200OK)]
    public ActionResult<BoardStateMappingDefaultsDto> GetStateMappingDefaults() =>
        Ok(new BoardStateMappingDefaultsDto(BoardStateMapping.AzureDevOpsDefaults));

    [HttpPut]
    [ProducesResponseType(typeof(BoardConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BoardConnectionDto>> SaveConnection(
        [FromBody] SaveBoardConnectionRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!IsSupportedOrganizationUrl(request.OrganizationUrl))
        {
            ModelState.AddModelError(
                nameof(request.OrganizationUrl),
                "Enter the HTTPS URL of an Azure DevOps organisation, for example https://dev.azure.com/contoso.");

            return ValidationProblem(ModelState);
        }

        var connection = await boardConnectionService.SaveConnectionAsync(request, ct);

        return Ok(connection);
    }

    /// <summary>Removes the connection and destroys the stored credential.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConnection(CancellationToken ct = default) =>
        await boardConnectionService.DeleteConnectionAsync(ct) ? NoContent() : NotFound();

    /// <summary>Checks the stored credential against the board without importing anything.</summary>
    [HttpPost("test")]
    [EnableRateLimiting("ai")]
    [ProducesResponseType(typeof(BoardConnectionTestResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BoardConnectionTestResult>> TestConnection(CancellationToken ct = default) =>
        Ok(await boardConnectionService.TestConnectionAsync(ct));

    /// <summary>
    /// Pulls work items from the board. Rate limited with the AI endpoints: a sync bursts calls
    /// at someone else's Azure DevOps organisation, and a loop would read as an attack.
    /// </summary>
    /// <param name="fullResync">Re-reads the whole backlog; worth it after a mapping change.</param>
    [HttpPost("sync")]
    [EnableRateLimiting("ai")]
    [ProducesResponseType(typeof(BoardSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BoardSyncResultDto>> Sync(
        [FromQuery] bool fullResync = false,
        CancellationToken ct = default)
    {
        try
        {
            var result = await boardConnectionService.SyncAsync(fullResync, ct);

            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            // No board connected is a state problem, not a bad payload.
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "No board connected",
                Detail = exception.Message
            });
        }
    }

    /// <summary>
    /// Imports an Azure DevOps query exported as CSV, for squads whose client will not issue a
    /// personal access token.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxImportUploadBytes)]
    [ProducesResponseType(typeof(BoardSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BoardSyncResultDto>> ImportFile(
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "No file received",
                Detail = "Choose the CSV exported from your Azure DevOps query."
            });
        }

        if (!HasCsvExtension(file.FileName))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unsupported file type",
                Detail =
                    "Only a .csv export is supported. In Azure DevOps open your query, then " +
                    "choose Export to CSV."
            });
        }

        // Excel writes these as UTF-8 with a BOM on Windows; without detection the mark sticks
        // to the first header and the ID column never matches.
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var csvContent = await reader.ReadToEndAsync(ct);

        var result = await boardConnectionService.ImportFromFileAsync(csvContent, ct);

        // File contents stay out of the logs: an export carries a client's work item titles.
        logger.LogInformation(
            "Board file import of {FileBytes} bytes produced {ImportedCount} item(s)",
            file.Length,
            result.ImportedCount);

        return Ok(result);
    }

    /// <summary>
    /// This value becomes the host of a server-side request, so an unrestricted one turns sync
    /// into an SSRF primitive reaching internal addresses the browser never could. HTTPS keeps
    /// the credential off the wire in clear text.
    /// </summary>
    private static bool IsSupportedOrganizationUrl(string organizationUrl)
    {
        if (!Uri.TryCreate(organizationUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        // visualstudio.com covers the older {organisation}.visualstudio.com form.
        return uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCsvExtension(string fileName) =>
        Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);
}
