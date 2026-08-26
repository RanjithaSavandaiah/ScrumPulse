namespace ScrumPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using System.Text.RegularExpressions;

public static class DbInitializer
{
    public static async Task EnsureSchemaUpToDateAsync(AppDbContext context)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await context.Database.OpenConnectionAsync();

            using var command = connection.CreateCommand();

            // 1. Ensure PullRequestReviewLogs table exists
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""PullRequestReviewLogs"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_PullRequestReviewLogs"" PRIMARY KEY,
                    ""WorkItemId"" TEXT NULL,
                    ""AuthorId"" TEXT NOT NULL,
                    ""ReviewerId"" TEXT NULL,
                    ""SprintId"" TEXT NULL,
                    ""PrNumber"" TEXT NOT NULL,
                    ""PrTitle"" TEXT NOT NULL,
                    ""PrUrl"" TEXT NOT NULL,
                    ""TotalCommentsCount"" INTEGER NOT NULL,
                    ""ActionableCommentsCount"" INTEGER NOT NULL,
                    ""ReviewSummary"" TEXT NOT NULL,
                    ""ReviewStatus"" TEXT NOT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    ""MergedAtUtc"" TEXT NULL,
                    ""UpdatedAtUtc"" TEXT NULL,
                    ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                );
            ";
            await command.ExecuteNonQueryAsync();

            // 1b. Check if IsDeleted column exists in PullRequestReviewLogs
            command.CommandText = "PRAGMA table_info(\"PullRequestReviewLogs\");";
            using var prReader = await command.ExecuteReaderAsync();
            var hasIsDeleted = false;
            while (await prReader.ReadAsync())
            {
                var colName = prReader["name"]?.ToString();
                if (string.Equals(colName, "IsDeleted", StringComparison.OrdinalIgnoreCase))
                {
                    hasIsDeleted = true;
                    break;
                }
            }
            await prReader.CloseAsync();

            if (!hasIsDeleted)
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"PullRequestReviewLogs\" ADD COLUMN \"IsDeleted\" INTEGER NOT NULL DEFAULT 0;");
            }

            // 2. Ensure TeamLeaves has LeaveSlot column
            command.CommandText = "PRAGMA table_info(\"TeamLeaves\");";
            using var reader = await command.ExecuteReaderAsync();
            var hasLeaveSlot = false;
            while (await reader.ReadAsync())
            {
                var colName = reader["name"]?.ToString();
                if (string.Equals(colName, "LeaveSlot", StringComparison.OrdinalIgnoreCase))
                {
                    hasLeaveSlot = true;
                    break;
                }
            }
            await reader.CloseAsync();

            if (!hasLeaveSlot)
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"TeamLeaves\" ADD COLUMN \"LeaveSlot\" TEXT NOT NULL DEFAULT 'FullDay';");
            }

            // 3. Ensure DailyStandups has SprintId column
            command.CommandText = "PRAGMA table_info(\"DailyStandups\");";
            using var reader2 = await command.ExecuteReaderAsync();
            var hasSprintId = false;
            while (await reader2.ReadAsync())
            {
                var colName = reader2["name"]?.ToString();
                if (string.Equals(colName, "SprintId", StringComparison.OrdinalIgnoreCase))
                {
                    hasSprintId = true;
                    break;
                }
            }
            await reader2.CloseAsync();

            if (!hasSprintId)
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"DailyStandups\" ADD COLUMN \"SprintId\" TEXT NULL;");
            }

            // 4. Ensure WorkItems has EstimatedHours column
            command.CommandText = "PRAGMA table_info(\"WorkItems\");";
            using var reader3 = await command.ExecuteReaderAsync();
            var hasEstimatedHours = false;
            while (await reader3.ReadAsync())
            {
                var colName = reader3["name"]?.ToString();
                if (string.Equals(colName, "EstimatedHours", StringComparison.OrdinalIgnoreCase))
                {
                    hasEstimatedHours = true;
                    break;
                }
            }
            await reader3.CloseAsync();

            if (!hasEstimatedHours)
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"WorkItems\" ADD COLUMN \"EstimatedHours\" REAL NULL;");
            }

            if (!wasOpen) await context.Database.CloseConnectionAsync();
        }
        catch
        {
            // Table or database initialization error handled gracefully
        }
    }

    public static async Task SeedAsync(AppDbContext context)
    {
        await EnsureSchemaUpToDateAsync(context);

        // Clean existing team members' names to prevent duplicate role suffix in brackets
        var existingMembers = await context.TeamMembers.ToListAsync();
        foreach (var member in existingMembers)
        {
            var cleanedName = Regex.Replace(member.Name, @"\s*\([^)]*\)", "").Trim();
            if (cleanedName != member.Name && !string.IsNullOrWhiteSpace(cleanedName))
            {
                member.Name = cleanedName;
            }
        }

        // Seed Default Team Members if database is newly initialized
        if (!await context.TeamMembers.AnyAsync())
        {
            var sm = new TeamMember { Name = "Ranjitha", Email = "ranjitha.sm@scrumpulse.io", Role = RoleType.ScrumMaster, Location = "Offshore", Avatar = "RS", ActiveWipLimit = 5 };
            var dev1 = new TeamMember { Name = "Kaushik", Email = "kaushik.dev@scrumpulse.io", Role = RoleType.Developer, Location = "Offshore", Avatar = "KD", ActiveWipLimit = 3 };
            var dev2 = new TeamMember { Name = "Athul", Email = "athul.dev@scrumpulse.io", Role = RoleType.Developer, Location = "Offshore", Avatar = "AD", ActiveWipLimit = 3 };
            var dev3 = new TeamMember { Name = "Venkat", Email = "venkat.dev@scrumpulse.io", Role = RoleType.Developer, Location = "Offshore", Avatar = "VD", ActiveWipLimit = 3 };
            var dev4 = new TeamMember { Name = "Suhaim", Email = "suhaim.dev@scrumpulse.io", Role = RoleType.Developer, Location = "Offshore", Avatar = "SD", ActiveWipLimit = 3 };
            var qa1 = new TeamMember { Name = "Angan", Email = "angan.qa@scrumpulse.io", Role = RoleType.QaEngineer, Location = "Offshore", Avatar = "AQ", ActiveWipLimit = 4 };
            var cdl = new TeamMember { Name = "Rahul", Email = "rahul.cdl@scrumpulse.io", Role = RoleType.Cdl, Location = "Offshore", Avatar = "RC", ActiveWipLimit = 5 };

            context.TeamMembers.AddRange(sm, dev1, dev2, dev3, dev4, qa1, cdl);
        }

        await context.SaveChangesAsync();
    }
}
