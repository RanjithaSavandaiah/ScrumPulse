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
        "Teams" => @"
            CREATE TABLE IF NOT EXISTS ""Teams"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_Teams"" PRIMARY KEY,
                ""Name"" character varying(150) NOT NULL,
                ""Slug"" character varying(80) NOT NULL,
                ""Description"" text NOT NULL DEFAULT '',
                ""JoinCode"" character varying(20) NOT NULL,
                ""IsActive"" boolean NOT NULL DEFAULT true,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Teams_Slug"" ON ""Teams"" (""Slug"");
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Teams_JoinCode"" ON ""Teams"" (""JoinCode"");
            CREATE INDEX IF NOT EXISTS ""IX_Teams_IsActive"" ON ""Teams"" (""IsActive"");
        ",
        "TeamLeaves" => @"
            CREATE TABLE IF NOT EXISTS ""TeamLeaves"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_TeamLeaves"" PRIMARY KEY,
                ""TeamMemberId"" uuid NOT NULL,
                ""StartDate"" timestamp with time zone NOT NULL,
                ""EndDate"" timestamp with time zone NOT NULL,
                ""Reason"" text NOT NULL DEFAULT '',
                ""LeaveType"" integer NOT NULL DEFAULT 0,
                ""LeaveSlot"" integer NOT NULL DEFAULT 0,
                ""Location"" text NOT NULL DEFAULT 'Offshore',
                ""IsApproved"" boolean NOT NULL DEFAULT true,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_TeamLeaves_TeamMemberId"" ON ""TeamLeaves"" (""TeamMemberId"");
            CREATE INDEX IF NOT EXISTS ""IX_TeamLeaves_IsApproved"" ON ""TeamLeaves"" (""IsApproved"");
        ",
        "PullRequestReviewLogs" => @"
            CREATE TABLE IF NOT EXISTS ""PullRequestReviewLogs"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_PullRequestReviewLogs"" PRIMARY KEY,
                ""WorkItemId"" uuid NULL,
                ""AuthorId"" uuid NOT NULL,
                ""ReviewerId"" uuid NULL,
                ""SprintId"" uuid NULL,
                ""PrNumber"" character varying(50) NOT NULL,
                ""PrTitle"" character varying(300) NOT NULL,
                ""PrUrl"" text NOT NULL DEFAULT '',
                ""TotalCommentsCount"" integer NOT NULL DEFAULT 0,
                ""ActionableCommentsCount"" integer NOT NULL DEFAULT 0,
                ""ReviewSummary"" text NOT NULL DEFAULT '',
                ""ReviewStatus"" integer NOT NULL DEFAULT 0,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""MergedAtUtc"" timestamp with time zone NULL,
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "Sprints" => @"
            CREATE TABLE IF NOT EXISTS ""Sprints"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_Sprints"" PRIMARY KEY,
                ""Name"" character varying(200) NOT NULL,
                ""Goal"" text NOT NULL DEFAULT '',
                ""StartDate"" timestamp with time zone NOT NULL,
                ""EndDate"" timestamp with time zone NOT NULL,
                ""IsActive"" boolean NOT NULL DEFAULT false,
                ""CommittedStoryPoints"" integer NOT NULL DEFAULT 0,
                ""ConfidenceScore"" integer NOT NULL DEFAULT 0,
                ""DailyHoursPerDeveloper"" double precision NOT NULL DEFAULT 8.5,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "TeamMembers" => @"
            CREATE TABLE IF NOT EXISTS ""TeamMembers"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_TeamMembers"" PRIMARY KEY,
                ""Name"" character varying(200) NOT NULL,
                ""Email"" character varying(200) NOT NULL,
                ""Role"" integer NOT NULL DEFAULT 0,
                ""Location"" text NOT NULL DEFAULT 'Offshore',
                ""Avatar"" text NOT NULL DEFAULT '',
                ""ActiveWipLimit"" integer NOT NULL DEFAULT 3,
                ""FocusArea"" text NOT NULL DEFAULT '',
                ""IsActive"" boolean NOT NULL DEFAULT true,
                ""WeeklyWorkingDays"" integer NOT NULL DEFAULT 5,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "WorkItems" => @"
            CREATE TABLE IF NOT EXISTS ""WorkItems"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_WorkItems"" PRIMARY KEY,
                ""Key"" character varying(50) NOT NULL,
                ""Title"" character varying(300) NOT NULL,
                ""Description"" text NOT NULL DEFAULT '',
                ""Type"" integer NOT NULL DEFAULT 0,
                ""Status"" integer NOT NULL DEFAULT 0,
                ""Priority"" integer NOT NULL DEFAULT 0,
                ""StoryPoints"" integer NOT NULL DEFAULT 0,
                ""EstimatedHours"" double precision NULL,
                ""AssigneeId"" uuid NULL,
                ""SprintId"" uuid NULL,
                ""PrReviewerId"" uuid NULL,
                ""DorAcceptanceCriteriaDefined"" boolean NOT NULL DEFAULT true,
                ""DorDependenciesIdentified"" boolean NOT NULL DEFAULT true,
                ""DorWireframeAvailable"" boolean NOT NULL DEFAULT true,
                ""DodUnitTestsPassed"" boolean NOT NULL DEFAULT false,
                ""DodPeerReviewCompleted"" boolean NOT NULL DEFAULT false,
                ""DodMergedToMaster"" boolean NOT NULL DEFAULT false,
                ""DodStagingVerified"" boolean NOT NULL DEFAULT false,
                ""IsEscapedDefect"" boolean NOT NULL DEFAULT false,
                ""PrUrl"" text NOT NULL DEFAULT '',
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "Blockers" => @"
            CREATE TABLE IF NOT EXISTS ""Blockers"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_Blockers"" PRIMARY KEY,
                ""Title"" character varying(300) NOT NULL,
                ""Description"" text NOT NULL DEFAULT '',
                ""Category"" integer NOT NULL DEFAULT 0,
                ""Severity"" integer NOT NULL DEFAULT 0,
                ""Impact"" integer NOT NULL DEFAULT 0,
                ""SprintId"" uuid NULL,
                ""WorkItemId"" uuid NULL,
                ""RaisedById"" uuid NULL,
                ""RaisedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""ResolvedAtUtc"" timestamp with time zone NULL,
                ""ResolutionNotes"" text NOT NULL DEFAULT '',
                ""SlaTargetHours"" double precision NOT NULL DEFAULT 24,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "DailyStandups" => @"
            CREATE TABLE IF NOT EXISTS ""DailyStandups"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_DailyStandups"" PRIMARY KEY,
                ""TeamMemberId"" uuid NOT NULL,
                ""SprintId"" uuid NULL,
                ""StandupDate"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""YesterdaySummary"" text NOT NULL DEFAULT '',
                ""TodayPlan"" text NOT NULL DEFAULT '',
                ""BlockersText"" text NOT NULL DEFAULT '',
                ""MoodScore"" integer NOT NULL DEFAULT 3,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "Monthly1on1Feedbacks" => @"
            CREATE TABLE IF NOT EXISTS ""Monthly1on1Feedbacks"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_Monthly1on1Feedbacks"" PRIMARY KEY,
                ""TeamMemberId"" uuid NOT NULL,
                ""MonthYear"" character varying(50) NOT NULL,
                ""ScrumMasterFeedback"" text NOT NULL DEFAULT '',
                ""CdlFeedback"" text NOT NULL DEFAULT '',
                ""ClientFeedback"" text NOT NULL DEFAULT '',
                ""SelfReflection"" text NOT NULL DEFAULT '',
                ""SmRating"" integer NOT NULL DEFAULT 3,
                ""HappinessIndex"" integer NOT NULL DEFAULT 3,
                ""ActionItems"" text NOT NULL DEFAULT '',
                ""NextMonthGoals"" text NOT NULL DEFAULT '',
                ""AiSynthesizedStrengths"" text NOT NULL DEFAULT '',
                ""AiGrowthRecommendations"" text NOT NULL DEFAULT '',
                ""AiBurnoutRiskAssessment"" text NOT NULL DEFAULT '',
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "RetroCards" => @"
            CREATE TABLE IF NOT EXISTS ""RetroCards"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_RetroCards"" PRIMARY KEY,
                ""SprintId"" uuid NOT NULL,
                ""Category"" integer NOT NULL DEFAULT 0,
                ""Content"" text NOT NULL DEFAULT '',
                ""AuthorId"" uuid NULL,
                ""IsAnonymous"" boolean NOT NULL DEFAULT false,
                ""UpvotesCount"" integer NOT NULL DEFAULT 0,
                ""UpvoterMemberIdsJson"" text NOT NULL DEFAULT '[]',
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "RetroActionItems" => @"
            CREATE TABLE IF NOT EXISTS ""RetroActionItems"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_RetroActionItems"" PRIMARY KEY,
                ""SprintId"" uuid NOT NULL,
                ""Title"" character varying(300) NOT NULL,
                ""AssigneeId"" uuid NULL,
                ""DueDate"" timestamp with time zone NULL,
                ""IsCompleted"" boolean NOT NULL DEFAULT false,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "KudosCards" => @"
            CREATE TABLE IF NOT EXISTS ""KudosCards"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_KudosCards"" PRIMARY KEY,
                ""SenderId"" uuid NOT NULL,
                ""ReceiverId"" uuid NOT NULL,
                ""Badge"" character varying(100) NOT NULL,
                ""Message"" text NOT NULL DEFAULT '',
                ""ReactionEmojisJson"" text NOT NULL DEFAULT '{}',
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "TechDebtItems" => @"
            CREATE TABLE IF NOT EXISTS ""TechDebtItems"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_TechDebtItems"" PRIMARY KEY,
                ""Title"" character varying(300) NOT NULL,
                ""Description"" text NOT NULL DEFAULT '',
                ""Severity"" integer NOT NULL DEFAULT 0,
                ""Status"" integer NOT NULL DEFAULT 0,
                ""EstimatedEffortHours"" double precision NOT NULL DEFAULT 0,
                ""Component"" character varying(200) NOT NULL DEFAULT '',
                ""SprintId"" uuid NULL,
                ""WorkItemId"" uuid NULL,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAtUtc"" timestamp with time zone NULL,
                ""CreatedBy"" text NULL,
                ""UpdatedBy"" text NULL,
                ""IsDeleted"" boolean NOT NULL DEFAULT false,
                ""RowVersion"" bytea NULL
            );
        ",
        "TechTalkLogs" => @"
            CREATE TABLE IF NOT EXISTS ""TechTalkLogs"" (
                ""Id"" uuid NOT NULL CONSTRAINT ""PK_TechTalkLogs"" PRIMARY KEY,
                ""Topic"" character varying(300) NOT NULL,
                ""SpeakerId"" uuid NOT NULL,
                ""ScheduledDate"" timestamp with time zone NOT NULL,
                ""DurationMinutes"" integer NOT NULL DEFAULT 60,
                ""SlidesUrl"" text NOT NULL DEFAULT '',
                ""RecordingUrl"" text NOT NULL DEFAULT '',
                ""FeedbackSummary"" text NOT NULL DEFAULT '',
                ""AverageRating"" double precision NOT NULL DEFAULT 0,
                ""TeamId"" uuid NULL,
                ""CreatedAtUtc"" timestamp with time zone NOT NULL DEFAULT NOW(),
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
