namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>Leave management with capacity calculation integration.</summary>
public class LeavesController(
    IAppDbContext db,
    IMetricsCalculatorService metricsCalculatorService,
    ILogger<LeavesController>? logger = null) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamLeaveDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamLeaveDto>>> GetAll(
        [FromQuery] Guid? memberId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct = default)
    {
        try
        {
            var query = db.TeamLeaves
                .IgnoreQueryFilters()
                .Include(leave => leave.TeamMember)
                .Where(l => l.IsDeleted != true)
                .AsQueryable();

            if (memberId.HasValue) query = query.Where(l => l.TeamMemberId == memberId.Value);
            if (year.HasValue && month.HasValue && year.Value >= 2000 && month.Value >= 1 && month.Value <= 12)
            {
                var startOfMonth = new DateTime(year.Value, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
                query = query.Where(l => l.StartDate <= endOfMonth && l.EndDate >= startOfMonth);
            }

            var list = await query
                .OrderByDescending(leave => leave.StartDate)
                .AsNoTracking()
                .ToListAsync(ct);

            if (list.Count > 0)
            {
                return Ok(list.ToDtos());
            }

            // If EF Core returned 0 items, check resilient ADO fallback in case legacy columns or rows were skipped
            var adoResults = await ExecuteResilientAdoFallback(memberId, year, month, ct);
            return Ok(adoResults.Count > 0 ? adoResults : list.ToDtos());
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load leaves with EF Core. Attempting resilient ADO fallback.");
            var fallbackLeaves = await ExecuteResilientAdoFallback(memberId, year, month, ct);
            return Ok(fallbackLeaves);
        }
    }

    [HttpGet("diagnostics")]
    public async Task<IActionResult> GetDiagnostics(CancellationToken ct = default)
    {
        var diag = new Dictionary<string, object?>();
        var dbContext = db as DbContext;
        if (dbContext != null && dbContext.Database.IsRelational())
        {
            var conn = dbContext.Database.GetDbConnection();
            diag["provider"] = dbContext.Database.ProviderName;
            diag["connectionType"] = conn.GetType().FullName;
            try
            {
                var wasOpen = conn.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await conn.OpenAsync(ct);

                using var countCmd = conn.CreateCommand();
                countCmd.CommandText = @"SELECT COUNT(*) FROM ""TeamLeaves"";";
                var count = await countCmd.ExecuteScalarAsync(ct);
                diag["rawLeavesCount"] = count;

                using var colsCmd = conn.CreateCommand();
                colsCmd.CommandText = @"SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE LOWER(table_name) = 'teamleaves';";
                var cols = new List<object>();
                using (var reader = await colsCmd.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        cols.Add(new { name = reader[0]?.ToString(), type = reader[1]?.ToString(), nullable = reader[2]?.ToString() });
                    }
                }
                diag["columns"] = cols;

                using var sampleCmd = conn.CreateCommand();
                sampleCmd.CommandText = @"SELECT ""Id"", ""TeamMemberId"", ""StartDate"", ""EndDate"", ""Reason"", ""LeaveType"", ""LeaveSlot"", ""Location"", ""IsApproved"", ""CreatedBy"", ""UpdatedBy"" FROM ""TeamLeaves"" LIMIT 5;";
                var samples = new List<object>();
                using (var sReader = await sampleCmd.ExecuteReaderAsync(ct))
                {
                    while (await sReader.ReadAsync(ct))
                    {
                        samples.Add(new {
                            id = sReader["Id"]?.ToString(),
                            memberId = sReader["TeamMemberId"]?.ToString(),
                            start = sReader["StartDate"]?.ToString(),
                            end = sReader["EndDate"]?.ToString(),
                            reason = sReader["Reason"]?.ToString(),
                            type = sReader["LeaveType"]?.ToString(),
                            slot = sReader["LeaveSlot"]?.ToString(),
                            location = sReader["Location"]?.ToString(),
                            approved = sReader["IsApproved"]?.ToString(),
                            createdBy = sReader["CreatedBy"]?.ToString(),
                            updatedBy = sReader["UpdatedBy"]?.ToString()
                        });
                    }
                }
                diag["samples"] = samples;

                if (!wasOpen) await conn.CloseAsync();
            }
            catch (Exception ex)
            {
                diag["adoError"] = ex.ToString();
            }
        }
        else if (dbContext != null)
        {
            diag["provider"] = dbContext.Database.ProviderName;
        }

        try
        {
            var efCount = await db.TeamLeaves.IgnoreQueryFilters().CountAsync(ct);
            diag["efLeavesCount"] = efCount;
        }
        catch (Exception efEx)
        {
            diag["efError"] = efEx.ToString();
        }

        return Ok(diag);
    }

    private async Task<List<TeamLeaveDto>> ExecuteResilientAdoFallback(
        Guid? memberId, int? year, int? month, CancellationToken ct)
    {
        var results = new List<TeamLeaveDto>();
        var dbContext = db as DbContext;
        if (dbContext == null || !dbContext.Database.IsRelational()) return results;

        var conn = dbContext.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);

        try
        {
            // 1. If PostgreSQL, ensure all columns exist and backfill NULLs
            if (conn.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                var ddlStatements = new[]
                {
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""TeamId"" uuid NULL;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""LeaveSlot"" text NOT NULL DEFAULT 'FullDay';",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""CreatedBy"" text NULL;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""UpdatedBy"" text NULL;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""IsDeleted"" boolean NOT NULL DEFAULT false;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""Location"" text NOT NULL DEFAULT 'Offshore';",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""Reason"" text NOT NULL DEFAULT 'Planned Leave';",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""IsApproved"" boolean NOT NULL DEFAULT true;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""LeaveType"" text NOT NULL DEFAULT 'PrivilegeLeave';",
                    @"UPDATE ""TeamLeaves"" SET ""IsDeleted"" = false WHERE ""IsDeleted"" IS NULL;",
                    @"UPDATE ""TeamLeaves"" SET ""IsApproved"" = true WHERE ""IsApproved"" IS NULL;",
                    @"UPDATE ""TeamLeaves"" SET ""LeaveSlot"" = 'FullDay' WHERE ""LeaveSlot"" IS NULL OR ""LeaveSlot"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""LeaveType"" = 'PrivilegeLeave' WHERE ""LeaveType"" IS NULL OR ""LeaveType"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""Location"" = 'Offshore' WHERE ""Location"" IS NULL OR ""Location"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""Reason"" = 'Planned Leave' WHERE ""Reason"" IS NULL OR ""Reason"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""CreatedBy"" = 'Scrum Master' WHERE ""CreatedBy"" IS NULL OR ""CreatedBy"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""UpdatedBy"" = 'Scrum Master' WHERE ""UpdatedBy"" IS NULL OR ""UpdatedBy"" = '';"
                };

                foreach (var ddl in ddlStatements)
                {
                    try
                    {
                        using var ddlCmd = conn.CreateCommand();
                        ddlCmd.CommandText = ddl;
                        await ddlCmd.ExecuteNonQueryAsync(ct);
                    }
                    catch
                    {
                        try
                        {
                            using var rb = conn.CreateCommand();
                            rb.CommandText = "ROLLBACK;";
                            await rb.ExecuteNonQueryAsync(ct);
                        }
                        catch { }
                    }
                }
            }

            // 2. Load team members mapping
            var membersMap = new Dictionary<Guid, string>();
            try
            {
                using var memCmd = conn.CreateCommand();
                memCmd.CommandText = @"SELECT ""Id"", ""Name"" FROM ""TeamMembers"";";
                using var memReader = await memCmd.ExecuteReaderAsync(ct);
                while (await memReader.ReadAsync(ct))
                {
                    var rawId = memReader["Id"];
                    if (rawId is Guid gid)
                    {
                        membersMap[gid] = memReader["Name"]?.ToString() ?? "Member";
                    }
                    else if (Guid.TryParse(rawId?.ToString(), out var parsedGid))
                    {
                        membersMap[parsedGid] = memReader["Name"]?.ToString() ?? "Member";
                    }
                }
            }
            catch { }

            // 3. Query all columns from TeamLeaves with resilient COALESCE and safe parsing
            using (var queryCmd = conn.CreateCommand())
            {
                queryCmd.CommandText = @"
                    SELECT 
                        ""Id"", 
                        ""TeamMemberId"", 
                        ""StartDate"", 
                        ""EndDate"", 
                        COALESCE(""Reason"", 'Planned Leave') AS ""Reason"",
                        COALESCE(""LeaveType"", 'PrivilegeLeave') AS ""LeaveType"",
                        COALESCE(""LeaveSlot"", 'FullDay') AS ""LeaveSlot"",
                        COALESCE(""Location"", 'Offshore') AS ""Location"",
                        COALESCE(""IsApproved"", true) AS ""IsApproved"",
                        COALESCE(""CreatedBy"", 'Scrum Master') AS ""CreatedBy"",
                        COALESCE(""UpdatedBy"", 'Scrum Master') AS ""UpdatedBy""
                    FROM ""TeamLeaves""
                    ORDER BY ""StartDate"" DESC;
                ";

                using var reader = await queryCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    try
                    {
                        var rawId = reader["Id"];
                        var id = rawId is Guid g ? g : Guid.Parse(rawId.ToString()!);
                        var rawMemberId = reader["TeamMemberId"];
                        var tmId = rawMemberId is Guid mg ? mg : Guid.Parse(rawMemberId.ToString()!);

                        var startObj = reader["StartDate"];
                        var startDate = startObj is DateTime sdt
                            ? (sdt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(sdt, DateTimeKind.Utc) : sdt.ToUniversalTime())
                            : DateTime.SpecifyKind(DateTime.Parse(startObj.ToString()!), DateTimeKind.Utc);

                        var endObj = reader["EndDate"];
                        var endDate = endObj is DateTime edt
                            ? (edt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(edt, DateTimeKind.Utc) : edt.ToUniversalTime())
                            : DateTime.SpecifyKind(DateTime.Parse(endObj.ToString()!), DateTimeKind.Utc);

                        var reason = reader["Reason"]?.ToString() ?? "Planned Leave";

                        var rawType = reader["LeaveType"]?.ToString();
                        var leaveType = int.TryParse(rawType, out var typeInt) && Enum.IsDefined(typeof(LeaveCategory), typeInt)
                            ? ((LeaveCategory)typeInt).ToString()
                            : (Enum.TryParse<LeaveCategory>(rawType, true, out var parsedCategory) ? parsedCategory.ToString() : "PrivilegeLeave");

                        var rawSlot = reader["LeaveSlot"]?.ToString();
                        var leaveSlot = int.TryParse(rawSlot, out var slotInt) && Enum.IsDefined(typeof(LeaveSlotType), slotInt)
                            ? ((LeaveSlotType)slotInt).ToString()
                            : (Enum.TryParse<LeaveSlotType>(rawSlot, true, out var parsedSlot) ? parsedSlot.ToString() : "FullDay");

                        var location = reader["Location"]?.ToString() ?? "Offshore";
                        var isApproved = reader["IsApproved"] is bool app ? app : true;
                        var createdBy = reader["CreatedBy"]?.ToString() ?? "Scrum Master";
                        var updatedBy = reader["UpdatedBy"]?.ToString() ?? "Scrum Master";

                        double totalDays = 1.0;
                        if (leaveSlot == "FirstHalf" || leaveSlot == "SecondHalf")
                        {
                            totalDays = 0.5;
                        }
                        else
                        {
                            int bDays = 0;
                            var cur = startDate.Date;
                            var end = endDate.Date;
                            while (cur <= end && bDays < 365)
                            {
                                if (cur.DayOfWeek != DayOfWeek.Saturday && cur.DayOfWeek != DayOfWeek.Sunday) bDays++;
                                cur = cur.AddDays(1);
                            }
                            totalDays = Math.Max(1, bDays);
                        }

                        membersMap.TryGetValue(tmId, out var memName);
                        results.Add(new TeamLeaveDto(
                            id, tmId, memName ?? "Squad Member",
                            startDate, endDate, reason, leaveType, location,
                            isApproved, totalDays, leaveSlot, createdBy, updatedBy
                        ));
                    }
                    catch (Exception parseEx)
                    {
                        logger?.LogWarning(parseEx, "Skipping corrupted leave row during ADO fallback read.");
                    }
                }
            }

            if (memberId.HasValue)
            {
                results = results.Where(l => l.TeamMemberId == memberId.Value).ToList();
            }
            if (year.HasValue && month.HasValue && year.Value >= 2000 && month.Value >= 1 && month.Value <= 12)
            {
                var startOfMonth = new DateTime(year.Value, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
                results = results.Where(l => l.StartDate <= endOfMonth && l.EndDate >= startOfMonth).ToList();
            }
        }
        catch (Exception adoEx)
        {
            logger?.LogError(adoEx, "ADO fallback query failed: {Message}", adoEx.Message);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }

        return results;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TeamLeaveDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamLeaveDto>> Submit([FromBody] SubmitLeaveRequest request, CancellationToken ct = default)
    {
        var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        var rawEnd = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;
        var endDate = DateTime.SpecifyKind(rawEnd, DateTimeKind.Utc);

        var leave = new TeamLeave
        {
            TeamMemberId = request.TeamMemberId,
            StartDate = startDate,
            EndDate = endDate,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim(),
            LeaveType = Enum.TryParse<LeaveCategory>(request.LeaveType, true, out var parsed) ? parsed : LeaveCategory.PrivilegeLeave,
            LeaveSlot = Enum.TryParse<LeaveSlotType>(request.LeaveSlot, true, out var slot) ? slot : LeaveSlotType.FullDay,
            Location = string.IsNullOrWhiteSpace(request.Location) ? "Offshore" : request.Location.Trim(),
            IsApproved = true
        };
        db.TeamLeaves.Add(leave);
        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId, ct);
        leave.TeamMember = member;

        return Ok(leave.ToDto());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TeamLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamLeaveDto>> Update(Guid id, [FromBody] SubmitLeaveRequest request, CancellationToken ct = default)
    {
        var leave = await db.TeamLeaves.FindAsync([id], ct);
        if (leave == null) return NotFound();

        var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        var rawEnd = request.EndDate < request.StartDate ? request.StartDate : request.EndDate;
        var endDate = DateTime.SpecifyKind(rawEnd, DateTimeKind.Utc);

        leave.TeamMemberId = request.TeamMemberId;
        leave.StartDate = startDate;
        leave.EndDate = endDate;
        leave.Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Planned Leave" : request.Reason.Trim();
        leave.LeaveType = Enum.TryParse<LeaveCategory>(request.LeaveType, true, out var parsed) ? parsed : LeaveCategory.PrivilegeLeave;
        leave.LeaveSlot = Enum.TryParse<LeaveSlotType>(request.LeaveSlot, true, out var slot) ? slot : LeaveSlotType.FullDay;
        if (!string.IsNullOrWhiteSpace(request.Location)) leave.Location = request.Location.Trim();

        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.Id == request.TeamMemberId, ct);
        leave.TeamMember = member;

        return Ok(leave.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var leave = await db.TeamLeaves.FindAsync([id], ct);
        if (leave == null) return NotFound();
        db.TeamLeaves.Remove(leave);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("capacity/{sprintId:guid}")]
    [HttpGet("sprint/{sprintId:guid}/capacity")]
    [ProducesResponseType(typeof(SprintCapacityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SprintCapacityDto>> GetCapacity(Guid sprintId, CancellationToken ct = default) =>
        Ok(await metricsCalculatorService.CalculateSprintCapacityAsync(sprintId, ct));
}
