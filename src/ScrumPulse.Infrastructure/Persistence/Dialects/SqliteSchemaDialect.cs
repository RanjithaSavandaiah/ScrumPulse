namespace ScrumPulse.Infrastructure.Persistence.Dialects;

using System.Data.Common;

/// <summary>
/// SQLite dialect strategy implementing schema inspection, data type mappings,
/// and ALTER TABLE syntax tailored for SQLite.
/// </summary>
public class SqliteSchemaDialect : ISchemaDialect
{
    public string GetExistingColumnsSql(string tableName) =>
        $"PRAGMA table_info(\"{tableName}\");";

    public string ReadColumnName(DbDataReader reader) =>
        reader["name"]?.ToString() ?? string.Empty;

    public string? MapToSqlType(Type clrType, bool isNullable)
    {
        if (clrType == typeof(bool))
            return isNullable ? "INTEGER NULL" : "INTEGER NOT NULL DEFAULT 0";

        if (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short) || clrType.IsEnum)
            return isNullable ? "INTEGER NULL" : "INTEGER NOT NULL DEFAULT 0";

        if (clrType == typeof(double) || clrType == typeof(float) || clrType == typeof(decimal))
            return isNullable ? "REAL NULL" : "REAL NOT NULL DEFAULT 0";

        if (clrType == typeof(byte[]))
            return "BLOB NULL";

        if (clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset))
            return isNullable ? "TEXT NULL" : "TEXT NOT NULL DEFAULT '1970-01-01 00:00:00'";

        if (clrType == typeof(Guid))
            return isNullable ? "TEXT NULL" : "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'";

        return isNullable ? "TEXT NULL" : "TEXT NOT NULL DEFAULT ''";
    }

    public string BuildAddColumnSql(string tableName, string columnName, string sqlType) =>
        $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {sqlType};";

    public string? GetInitialTableDdl(string tableName) => tableName switch
    {
        "PullRequestReviewLogs" => @"
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
                ""ReviewStatus"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""MergedAtUtc"" TEXT NULL,
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        _ => null
    };
}
