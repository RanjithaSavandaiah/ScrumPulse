namespace ScrumPulse.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence.Dialects;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

public static class DbInitializer
{
    public static async Task EnsureSchemaUpToDateAsync(AppDbContext context, ILogger? logger = null)
    {
        // Only relational databases support raw DDL schema migrations
        if (!context.Database.IsRelational())
        {
            return;
        }

        var dialect = SchemaDialectFactory.GetDialect(context.Database);
        if (dialect == null)
        {
            return;
        }

        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await context.Database.OpenConnectionAsync();

        try
        {
            // -- Explicit Raw DDL Repair for Critical Tables (TeamLeaves, Teams, etc.) --
            if (dialect is PostgresSchemaDialect)
            {
                var rawMigrations = new[]
                {
                    @"CREATE TABLE IF NOT EXISTS ""Teams"" (""Id"" uuid NOT NULL CONSTRAINT ""PK_Teams"" PRIMARY KEY, ""Name"" character varying(150) NOT NULL, ""Slug"" character varying(80) NOT NULL, ""Description"" text NOT NULL DEFAULT '', ""JoinCode"" character varying(20) NOT NULL, ""IsActive"" boolean NOT NULL DEFAULT true, ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW());",
                    @"CREATE TABLE IF NOT EXISTS ""TeamLeaves"" (""Id"" uuid NOT NULL CONSTRAINT ""PK_TeamLeaves"" PRIMARY KEY, ""TeamMemberId"" uuid NOT NULL, ""StartDate"" timestamp with time zone NOT NULL, ""EndDate"" timestamp with time zone NOT NULL, ""Reason"" text NOT NULL DEFAULT '', ""LeaveType"" integer NOT NULL DEFAULT 0, ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW());",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""TeamId"" uuid NULL;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""LeaveSlot"" integer NOT NULL DEFAULT 0;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""CreatedBy"" text NULL;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""UpdatedBy"" text NULL;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""IsDeleted"" boolean NOT NULL DEFAULT false;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""Location"" text NOT NULL DEFAULT 'Offshore';",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""Reason"" text NOT NULL DEFAULT 'Planned Leave';",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""IsApproved"" boolean NOT NULL DEFAULT true;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""LeaveType"" integer NOT NULL DEFAULT 0;",
                    @"ALTER TABLE ""TeamLeaves"" ADD COLUMN IF NOT EXISTS ""RowVersion"" bytea NULL;",
                    @"ALTER TABLE ""WorkItems"" ADD COLUMN IF NOT EXISTS ""TeamId"" uuid NULL;",
                    @"ALTER TABLE ""Blockers"" ADD COLUMN IF NOT EXISTS ""TeamId"" uuid NULL;",
                    @"ALTER TABLE ""TeamMembers"" ADD COLUMN IF NOT EXISTS ""TeamId"" uuid NULL;",
                    @"ALTER TABLE ""Sprints"" ADD COLUMN IF NOT EXISTS ""TeamId"" uuid NULL;",
                    @"ALTER TABLE ""DailyStandups"" ADD COLUMN IF NOT EXISTS ""SprintId"" uuid NULL;",
                    @"ALTER TABLE ""DailyStandups"" ADD COLUMN IF NOT EXISTS ""TeamId"" uuid NULL;",
                    @"ALTER TABLE ""WorkItems"" ADD COLUMN IF NOT EXISTS ""IsEscapedDefect"" boolean NOT NULL DEFAULT false;",
                    @"ALTER TABLE ""WorkItems"" ADD COLUMN IF NOT EXISTS ""DefectRootCause"" text NULL;",
                    @"CREATE TABLE IF NOT EXISTS ""KudosCards"" (""Id"" uuid NOT NULL CONSTRAINT ""PK_KudosCards"" PRIMARY KEY, ""SenderId"" uuid NOT NULL, ""ReceiverId"" uuid NOT NULL, ""Badge"" character varying(100) NOT NULL, ""Message"" text NOT NULL DEFAULT '', ""ReactionEmojisJson"" text NOT NULL DEFAULT '{}', ""TeamId"" uuid NULL, ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(), ""UpdatedAtUtc"" timestamp with time zone NULL, ""CreatedBy"" text NULL, ""UpdatedBy"" text NULL, ""IsDeleted"" boolean NOT NULL DEFAULT false, ""RowVersion"" bytea NULL);",
                    @"CREATE TABLE IF NOT EXISTS ""TechDebtItems"" (""Id"" uuid NOT NULL CONSTRAINT ""PK_TechDebtItems"" PRIMARY KEY, ""Title"" character varying(300) NOT NULL, ""Description"" text NOT NULL DEFAULT '', ""Severity"" integer NOT NULL DEFAULT 0, ""Status"" integer NOT NULL DEFAULT 0, ""EstimatedEffortHours"" double precision NOT NULL DEFAULT 0, ""Component"" character varying(200) NOT NULL DEFAULT '', ""SprintId"" uuid NULL, ""WorkItemId"" uuid NULL, ""TeamId"" uuid NULL, ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(), ""UpdatedAtUtc"" timestamp with time zone NULL, ""CreatedBy"" text NULL, ""UpdatedBy"" text NULL, ""IsDeleted"" boolean NOT NULL DEFAULT false, ""RowVersion"" bytea NULL);",
                    @"CREATE TABLE IF NOT EXISTS ""TechTalkLogs"" (""Id"" uuid NOT NULL CONSTRAINT ""PK_TechTalkLogs"" PRIMARY KEY, ""Topic"" character varying(300) NOT NULL, ""SpeakerId"" uuid NOT NULL, ""ScheduledDate"" timestamp with time zone NOT NULL, ""DurationMinutes"" integer NOT NULL DEFAULT 60, ""SlidesUrl"" text NOT NULL DEFAULT '', ""RecordingUrl"" text NOT NULL DEFAULT '', ""FeedbackSummary"" text NOT NULL DEFAULT '', ""AverageRating"" double precision NOT NULL DEFAULT 0, ""TeamId"" uuid NULL, ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(), ""UpdatedAtUtc"" timestamp with time zone NULL, ""CreatedBy"" text NULL, ""UpdatedBy"" text NULL, ""IsDeleted"" boolean NOT NULL DEFAULT false, ""RowVersion"" bytea NULL);",
                    @"CREATE TABLE IF NOT EXISTS ""RetroCards"" (""Id"" uuid NOT NULL CONSTRAINT ""PK_RetroCards"" PRIMARY KEY, ""SprintId"" uuid NOT NULL, ""AuthorId"" uuid NOT NULL, ""Category"" integer NOT NULL DEFAULT 0, ""Content"" text NOT NULL DEFAULT '', ""VoteCount"" integer NOT NULL DEFAULT 0, ""TeamId"" uuid NULL, ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(), ""UpdatedAtUtc"" timestamp with time zone NULL, ""CreatedBy"" text NULL, ""UpdatedBy"" text NULL, ""IsDeleted"" boolean NOT NULL DEFAULT false, ""RowVersion"" bytea NULL);",
                    @"CREATE TABLE IF NOT EXISTS ""RetroActionItems"" (""Id"" uuid NOT NULL CONSTRAINT ""PK_RetroActionItems"" PRIMARY KEY, ""SprintId"" uuid NOT NULL, ""Description"" text NOT NULL DEFAULT '', ""OwnerId"" uuid NOT NULL, ""Status"" integer NOT NULL DEFAULT 0, ""DueDate"" timestamp with time zone NOT NULL, ""TeamId"" uuid NULL, ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(), ""UpdatedAtUtc"" timestamp with time zone NULL, ""CreatedBy"" text NULL, ""UpdatedBy"" text NULL, ""IsDeleted"" boolean NOT NULL DEFAULT false, ""RowVersion"" bytea NULL);",
                    @"CREATE TABLE IF NOT EXISTS ""Monthly1on1Feedbacks"" (""Id"" uuid NOT NULL CONSTRAINT ""PK_Monthly1on1Feedbacks"" PRIMARY KEY, ""TeamMemberId"" uuid NOT NULL, ""ReviewerId"" uuid NOT NULL, ""ReviewMonthYear"" character varying(20) NOT NULL, ""GoalProgressRating"" integer NOT NULL DEFAULT 3, ""GoalNotes"" text NOT NULL DEFAULT '', ""SprintVelocityAssessment"" text NOT NULL DEFAULT '', ""EngineeringGrowthNotes"" text NOT NULL DEFAULT '', ""ActionPlan"" text NOT NULL DEFAULT '', ""OverallSentimentScore"" integer NOT NULL DEFAULT 3, ""TeamId"" uuid NULL, ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(), ""UpdatedAtUtc"" timestamp with time zone NULL, ""CreatedBy"" text NULL, ""UpdatedBy"" text NULL, ""IsDeleted"" boolean NOT NULL DEFAULT false, ""RowVersion"" bytea NULL);",
                    @"UPDATE ""TeamLeaves"" SET ""IsDeleted"" = false WHERE ""IsDeleted"" IS NULL;",
                    @"UPDATE ""TeamLeaves"" SET ""IsApproved"" = true WHERE ""IsApproved"" IS NULL;",
                    @"UPDATE ""TeamLeaves"" SET ""LeaveSlot"" = 'FullDay' WHERE ""LeaveSlot"" IS NULL OR ""LeaveSlot"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""LeaveType"" = 'PrivilegeLeave' WHERE ""LeaveType"" IS NULL OR ""LeaveType"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""Location"" = 'Offshore' WHERE ""Location"" IS NULL OR ""Location"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""Reason"" = 'Planned Leave' WHERE ""Reason"" IS NULL OR ""Reason"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""CreatedBy"" = 'Scrum Master' WHERE ""CreatedBy"" IS NULL OR ""CreatedBy"" = '';",
                    @"UPDATE ""TeamLeaves"" SET ""UpdatedBy"" = 'Scrum Master' WHERE ""UpdatedBy"" IS NULL OR ""UpdatedBy"" = '';",
                    @"CREATE INDEX IF NOT EXISTS ""IX_TeamLeaves_TeamMemberId"" ON ""TeamLeaves"" (""TeamMemberId"");",
                    @"CREATE INDEX IF NOT EXISTS ""IX_TeamLeaves_IsApproved"" ON ""TeamLeaves"" (""IsApproved"");",
                    @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Teams_Slug"" ON ""Teams"" (""Slug"");",
                    @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Teams_JoinCode"" ON ""Teams"" (""JoinCode"");",
                    @"CREATE INDEX IF NOT EXISTS ""IX_Teams_IsActive"" ON ""Teams"" (""IsActive"");"
                };

                foreach (var sql in rawMigrations)
                {
                    if (string.IsNullOrWhiteSpace(sql)) continue;
                    try
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = sql;
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to execute raw migration SQL: {Sql}", sql);
                        try
                        {
                            using var rbCmd = connection.CreateCommand();
                            rbCmd.CommandText = "ROLLBACK;";
                            await rbCmd.ExecuteNonQueryAsync();
                        }
                        catch (Exception rbEx)
                        {
                            logger?.LogDebug(rbEx, "Failed to rollback after raw migration error");
                        }
                    }
                }
            }

            // Dynamically verify and migrate all tables and columns from EF Core model metadata
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (string.IsNullOrEmpty(tableName)) continue;

                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    using var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = dialect.GetExistingColumnsSql(tableName);
                    using var reader = await checkCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var colName = dialect.ReadColumnName(reader);
                        if (!string.IsNullOrEmpty(colName))
                        {
                            existingColumns.Add(colName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Table check query failed for {TableName}; likely does not exist yet", tableName);
                }

                // If table does not exist, create it using dialect initial DDL
                if (existingColumns.Count == 0)
                {
                    var initialDdl = dialect.GetInitialTableDdl(tableName);
                    if (!string.IsNullOrEmpty(initialDdl))
                    {
                        try
                        {
                            using var createCmd = connection.CreateCommand();
                            createCmd.CommandText = initialDdl;
                            await createCmd.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Initial table creation DDL failed for {TableName}", tableName);
                            // Table might have been created concurrently
                            if (dialect is PostgresSchemaDialect)
                            {
                                try
                                {
                                    using var rb = connection.CreateCommand();
                                    rb.CommandText = "ROLLBACK;";
                                    await rb.ExecuteNonQueryAsync();
                                }
                                catch (Exception rbEx)
                                {
                                    logger?.LogDebug(rbEx, "Failed to rollback after table create failure for {TableName}", tableName);
                                }
                            }
                        }

                        // Re-query existing columns now that the table was created
                        try
                        {
                            using var verifyCmd = connection.CreateCommand();
                            verifyCmd.CommandText = dialect.GetExistingColumnsSql(tableName);
                            using var reader = await verifyCmd.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                var colName = dialect.ReadColumnName(reader);
                                if (!string.IsNullOrEmpty(colName))
                                {
                                    existingColumns.Add(colName);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogDebug(ex, "Verifying columns after table creation failed for {TableName}", tableName);
                            if (dialect is PostgresSchemaDialect)
                            {
                                try
                                {
                                    using var rb = connection.CreateCommand();
                                    rb.CommandText = "ROLLBACK;";
                                    await rb.ExecuteNonQueryAsync();
                                }
                                catch (Exception rbEx)
                                {
                                    logger?.LogDebug(rbEx, "Failed to rollback after verify failure for {TableName}", tableName);
                                }
                            }
                        }
                    }
                }

                // Check each scalar property defined on the entity
                var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);

                foreach (var property in entityType.GetProperties())
                {
                    var columnName = storeObject.HasValue
                        ? property.GetColumnName(storeObject.Value)
                        : property.GetColumnName() ?? property.Name;

                    if (string.IsNullOrEmpty(columnName) || existingColumns.Contains(columnName))
                    {
                        continue;
                    }

                    var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                    var sqlType = dialect.MapToSqlType(clrType, property.IsNullable);

                    if (sqlType != null)
                    {
                        try
                        {
                            using var alterCmd = connection.CreateCommand();
                            alterCmd.CommandText = dialect.BuildAddColumnSql(tableName, columnName, sqlType);
                            await alterCmd.ExecuteNonQueryAsync();
                            existingColumns.Add(columnName);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogDebug(ex, "Adding column {ColumnName} to {TableName} failed; column may already exist", columnName, tableName);
                            // Ignore if column already exists or table cannot be altered
                            if (dialect is PostgresSchemaDialect)
                            {
                                try
                                {
                                    using var rb = connection.CreateCommand();
                                    rb.CommandText = "ROLLBACK;";
                                    await rb.ExecuteNonQueryAsync();
                                }
                                catch (Exception rbEx)
                                {
                                    logger?.LogDebug(rbEx, "Failed to rollback after add column failure for {TableName}.{ColumnName}", tableName, columnName);
                                }
                            }
                        }
                    }
                }

                // Backfill CreatedBy and UpdatedBy where NULL to repair existing legacy records
                if (existingColumns.Contains("CreatedBy") && existingColumns.Contains("UpdatedBy"))
                {
                    try
                    {
                        using var backfillCmd = connection.CreateCommand();
                        var fallbackUser = tableName == "TeamLeaves" ? "Scrum Master" : "System";
                        backfillCmd.CommandText = $"UPDATE \"{tableName}\" SET \"CreatedBy\" = '{fallbackUser}', \"UpdatedBy\" = '{fallbackUser}' WHERE \"CreatedBy\" IS NULL OR \"CreatedBy\" = '';";
                        await backfillCmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        logger?.LogDebug(ex, "Backfill of audit columns failed for {TableName}", tableName);
                        // Ignore backfill error if table is empty or cannot be updated
                        if (dialect is PostgresSchemaDialect)
                        {
                            try
                            {
                                using var rb = connection.CreateCommand();
                                rb.CommandText = "ROLLBACK;";
                                await rb.ExecuteNonQueryAsync();
                            }
                            catch (Exception rbEx)
                            {
                                logger?.LogDebug(rbEx, "Failed to rollback after backfill failure for {TableName}", tableName);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Database schema initialization failed");
        }
        finally
        {
            if (!wasOpen) await context.Database.CloseConnectionAsync();
        }
    }

    public static async Task SeedAsync(AppDbContext context, bool seedDemoData = false, ILogger? logger = null)
    {
        await EnsureSchemaUpToDateAsync(context, logger);

        // Remove any legacy auto-seeded "Core Engineering Squad"
        var legacyCoreSquads = await context.Teams
            .Where(t => t.Slug == "core-engineering" || t.Name == "Core Engineering Squad")
            .ToListAsync();
        if (legacyCoreSquads.Count > 0)
        {
            var legacyIds = legacyCoreSquads.Select(s => s.Id).ToList();
            var membersLinked = await context.TeamMembers
                .Where(m => m.TeamId.HasValue && legacyIds.Contains(m.TeamId.Value))
                .ToListAsync();
            foreach (var m in membersLinked)
            {
                m.TeamId = null;
            }
            context.Teams.RemoveRange(legacyCoreSquads);
            await context.SaveChangesAsync();
        }

        // If unassigned members exist, automatically link them to the active squad (e.g. Fikacoders)
        var existingTeams = await context.Teams.Where(t => t.IsActive).ToListAsync();
        if (existingTeams.Count > 0)
        {
            var targetSquad = existingTeams.FirstOrDefault(t => t.Slug.Contains("fikacoders") || t.Name.Contains("Fikacoders", StringComparison.OrdinalIgnoreCase)) ?? existingTeams[0];
            var unassignedMembers = await context.TeamMembers
                .Where(m => m.TeamId == null && m.IsActive && !m.IsDeleted)
                .ToListAsync();
            if (unassignedMembers.Count > 0)
            {
                foreach (var member in unassignedMembers)
                {
                    member.TeamId = targetSquad.Id;
                }
                await context.SaveChangesAsync();
            }
        }

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

        // For public hosting, teams start with a clean squad roster so Scrum Masters
        // can create and configure their own team members.
        // If demo data is disabled (default in production), soft-delete any previously seeded demo members
        // so legacy demo members from earlier deployments do not appear on production.
        if (!seedDemoData)
        {
            var demoEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ranjitha.sm@scrumpulse.io",
                "kaushik.dev@scrumpulse.io",
                "athul.dev@scrumpulse.io",
                "venkat.dev@scrumpulse.io",
                "suhaim.dev@scrumpulse.io",
                "angan.qa@scrumpulse.io",
                "rahul.cdl@scrumpulse.io",
                "sm@scrumpulse.io",
                "dev1@scrumpulse.io",
                "dev2@scrumpulse.io",
                "qa@scrumpulse.io"
            };

            var legacyDemoMembers = await context.TeamMembers
                .Where(m => demoEmails.Contains(m.Email))
                .ToListAsync();

            if (legacyDemoMembers.Count > 0)
            {
                foreach (var member in legacyDemoMembers)
                {
                    member.IsDeleted = true;
                    member.IsActive = false;
                }
            }
        }
        else
        {
            // Ensure demo team exists
            var demoTeam = await context.Teams.FirstOrDefaultAsync();
            if (demoTeam == null)
            {
                demoTeam = new Team
                {
                    Name = "Fikacoders",
                    Slug = "fikacoders",
                    Description = "Primary Engineering Squad",
                    JoinCode = "FIKA123",
                    IsActive = true
                };
                context.Teams.Add(demoTeam);
                await context.SaveChangesAsync();
            }

            // Ensure demo active sprint exists
            if (!await context.Sprints.AnyAsync())
            {
                var now = DateTime.UtcNow;
                var sprint = new Sprint
                {
                    Name = "Sprint 1",
                    StartDate = now.AddDays(-7),
                    EndDate = now.AddDays(7),
                    Goal = "Deliver Sprint Core Features & Production Telemetry",
                    IsActive = true,
                    CommittedStoryPoints = 40,
                    TeamId = demoTeam.Id
                };
                context.Sprints.Add(sprint);
                await context.SaveChangesAsync();
            }

            var demoEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ranjitha.sm@scrumpulse.io",
                "kaushik.dev@scrumpulse.io",
                "athul.dev@scrumpulse.io",
                "venkat.dev@scrumpulse.io",
                "suhaim.dev@scrumpulse.io",
                "angan.qa@scrumpulse.io",
                "rahul.cdl@scrumpulse.io",
                "sm@scrumpulse.io",
                "dev1@scrumpulse.io",
                "dev2@scrumpulse.io",
                "qa@scrumpulse.io"
            };

            var softDeletedDemoMembers = await context.TeamMembers
                .IgnoreQueryFilters()
                .Where(m => demoEmails.Contains(m.Email))
                .ToListAsync();

            if (softDeletedDemoMembers.Count > 0)
            {
                foreach (var member in softDeletedDemoMembers)
                {
                    member.IsDeleted = false;
                    member.IsActive = true;
                    if (member.TeamId == null) member.TeamId = demoTeam.Id;
                }
            }
            else if (!await context.TeamMembers.AnyAsync())
            {
                var sm = new TeamMember { TeamId = demoTeam.Id, Name = "Scrum Master", Email = "sm@scrumpulse.io", Role = RoleType.ScrumMaster, Location = "Offshore", Avatar = "SM", ActiveWipLimit = 5 };
                var dev1 = new TeamMember { TeamId = demoTeam.Id, Name = "Developer 1", Email = "dev1@scrumpulse.io", Role = RoleType.Developer, Location = "Offshore", Avatar = "D1", ActiveWipLimit = 3 };
                var dev2 = new TeamMember { TeamId = demoTeam.Id, Name = "Developer 2", Email = "dev2@scrumpulse.io", Role = RoleType.Developer, Location = "Offshore", Avatar = "D2", ActiveWipLimit = 3 };
                var qa1 = new TeamMember { TeamId = demoTeam.Id, Name = "QA Engineer", Email = "qa@scrumpulse.io", Role = RoleType.QaEngineer, Location = "Offshore", Avatar = "QA", ActiveWipLimit = 4 };

                context.TeamMembers.AddRange(sm, dev1, dev2, qa1);
            }
        }

        await context.SaveChangesAsync();
    }
}
