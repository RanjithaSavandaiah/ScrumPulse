namespace ScrumPulse.Infrastructure.Persistence.Dialects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Factory resolving the appropriate database schema dialect based on EF Core provider configuration.
/// </summary>
public static class SchemaDialectFactory
{
    private static readonly SqliteSchemaDialect SqliteDialect = new();
    private static readonly PostgresSchemaDialect PostgresDialect = new();

    /// <summary>
    /// Returns the dialect strategy corresponding to the current database provider.
    /// </summary>
    public static ISchemaDialect? GetDialect(DatabaseFacade database)
    {
        if (database.IsSqlite())
        {
            return SqliteDialect;
        }

        var providerName = database.ProviderName;
        if (providerName != null &&
            (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
             providerName.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase)))
        {
            return PostgresDialect;
        }

        return null;
    }
}
