namespace ScrumPulse.Infrastructure.Persistence.Dialects;

using System.Data.Common;

/// <summary>
/// Strategy interface encapsulating database specific SQL dialect differences
/// for dynamic schema synchronization and migrations.
/// </summary>
public interface ISchemaDialect
{
    /// <summary>Returns SQL query to retrieve existing column names for a table.</summary>
    string GetExistingColumnsSql(string tableName);

    /// <summary>Reads column name from data reader result.</summary>
    string ReadColumnName(DbDataReader reader);

    /// <summary>Maps a .NET CLR type to provider specific SQL column type definition with safe default.</summary>
    string? MapToSqlType(Type clrType, bool isNullable);

    /// <summary>Builds provider specific ALTER TABLE ADD COLUMN SQL statement.</summary>
    string BuildAddColumnSql(string tableName, string columnName, string sqlType);

    /// <summary>Returns initial CREATE TABLE DDL if provider specific script is registered.</summary>
    string? GetInitialTableDdl(string tableName);
}
