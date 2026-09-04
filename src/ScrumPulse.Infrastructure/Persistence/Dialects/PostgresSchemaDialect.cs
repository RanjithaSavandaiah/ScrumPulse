namespace ScrumPulse.Infrastructure.Persistence.Dialects;

using System.Data.Common;

/// <summary>
/// PostgreSQL dialect strategy implementing schema inspection, native data type mappings,
/// and idempotent ALTER TABLE syntax tailored for PostgreSQL.
/// </summary>
public class PostgresSchemaDialect : ISchemaDialect
{
    public string GetExistingColumnsSql(string tableName) =>
        $"SELECT column_name FROM information_schema.columns WHERE LOWER(table_name) = LOWER('{tableName}');";

    public string ReadColumnName(DbDataReader reader) =>
        reader["column_name"]?.ToString() ?? string.Empty;

    public string? MapToSqlType(Type clrType, bool isNullable)
    {
        if (clrType == typeof(bool))
            return isNullable ? "boolean NULL" : "boolean NOT NULL DEFAULT false";

        if (clrType == typeof(int) || clrType == typeof(short) || clrType.IsEnum)
            return isNullable ? "integer NULL" : "integer NOT NULL DEFAULT 0";

        if (clrType == typeof(long))
            return isNullable ? "bigint NULL" : "bigint NOT NULL DEFAULT 0";

        if (clrType == typeof(double) || clrType == typeof(float))
            return isNullable ? "double precision NULL" : "double precision NOT NULL DEFAULT 0";

        if (clrType == typeof(decimal))
            return isNullable ? "numeric NULL" : "numeric NOT NULL DEFAULT 0";

        if (clrType == typeof(byte[]))
            return "bytea NULL";

        if (clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset))
            return isNullable ? "timestamp with time zone NULL" : "timestamp with time zone NOT NULL DEFAULT NOW()";

        if (clrType == typeof(Guid))
            return isNullable ? "uuid NULL" : "uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'";

        return isNullable ? "text NULL" : "text NOT NULL DEFAULT ''";
    }

    public string BuildAddColumnSql(string tableName, string columnName, string sqlType) =>
        $"ALTER TABLE \"{tableName}\" ADD COLUMN IF NOT EXISTS \"{columnName}\" {sqlType};";

    public string? GetInitialTableDdl(string tableName) => tableName switch
    {
        "PullRequestReviewLogs" => @"
            CREATE TABLE IF NOT EXISTS ""PullRequestReviewLogs"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_PullRequestReviewLogs"" PRIMARY KEY,
                ""WorkItemId"" uuid NULL,
                ""AuthorId"" uuid NOT NULL,
                ""ReviewerId"" uuid NULL,
                ""SprintId"" uuid NULL,
                ""PrNumber"" character varying(50) NOT NULL,
                ""PrTitle"" character varying(300) NOT NULL,
                ""PrUrl"" text NOT NULL,
                ""TotalCommentsCount"" integer NOT NULL,
                ""ActionableCommentsCount"" integer NOT NULL,
                ""ReviewSummary"" text NOT NULL,
                ""ReviewStatus"" integer NOT NULL DEFAULT 0,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL,
                ""MergedAtUtc"" timestamp with time zone NULL,
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        _ => null
    };
}
