namespace ScrumPulse.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence.Dialects;
using System.Text.RegularExpressions;

public static class DbInitializer
{
    public static async Task EnsureSchemaUpToDateAsync(AppDbContext context)
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
                catch
                {
                    // Table check query failed; likely does not exist
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
                        catch
                        {
                            // Table might have been created concurrently
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
                        catch
                        {
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
                        catch
                        {
                            // Ignore if column already exists or table cannot be altered
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
                    catch
                    {
                        // Ignore backfill error if table is empty or cannot be updated
                    }
                }
            }
        }
        catch
        {
            // Table or database initialization error handled gracefully
        }
        finally
        {
            if (!wasOpen) await context.Database.CloseConnectionAsync();
        }
    }

    public static async Task SeedAsync(AppDbContext context, bool seedDemoData = false)
    {
        await EnsureSchemaUpToDateAsync(context);

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
        else if (!await context.TeamMembers.AnyAsync())
        {
            var sm = new TeamMember { Name = "Scrum Master", Email = "sm@scrumpulse.io", Role = RoleType.ScrumMaster, Location = "Offshore", Avatar = "SM", ActiveWipLimit = 5 };
            var dev1 = new TeamMember { Name = "Developer 1", Email = "dev1@scrumpulse.io", Role = RoleType.Developer, Location = "Offshore", Avatar = "D1", ActiveWipLimit = 3 };
            var dev2 = new TeamMember { Name = "Developer 2", Email = "dev2@scrumpulse.io", Role = RoleType.Developer, Location = "Offshore", Avatar = "D2", ActiveWipLimit = 3 };
            var qa1 = new TeamMember { Name = "QA Engineer", Email = "qa@scrumpulse.io", Role = RoleType.QaEngineer, Location = "Offshore", Avatar = "QA", ActiveWipLimit = 4 };

            context.TeamMembers.AddRange(sm, dev1, dev2, qa1);
        }

        await context.SaveChangesAsync();
    }
}
