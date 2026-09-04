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
            using var command = connection.CreateCommand();

            // 1. Ensure required initial tables exist using provider dialect
            var createTableDdl = dialect.GetInitialTableDdl("PullRequestReviewLogs");
            if (!string.IsNullOrEmpty(createTableDdl))
            {
                command.CommandText = createTableDdl;
                await command.ExecuteNonQueryAsync();
            }

            // 2. Dynamically verify and migrate all tables and columns from EF Core model metadata
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (string.IsNullOrEmpty(tableName)) continue;

                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                command.CommandText = dialect.GetExistingColumnsSql(tableName);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var colName = dialect.ReadColumnName(reader);
                        if (!string.IsNullOrEmpty(colName))
                        {
                            existingColumns.Add(colName);
                        }
                    }
                }

                if (existingColumns.Count == 0)
                {
                    continue;
                }

                // Check each property defined on the entity
                var storeObject = StoreObjectIdentifier.Create(
                    entityType, StoreObjectType.Table);

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
                            command.CommandText = dialect.BuildAddColumnSql(tableName, columnName, sqlType);
                            await command.ExecuteNonQueryAsync();
                        }
                        catch
                        {
                            // Ignore if column already exists or table cannot be altered
                        }
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
        // Demo squad members are only seeded when explicitly enabled (e.g. SeedDemoData=true).
        if (seedDemoData && !await context.TeamMembers.AnyAsync())
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
