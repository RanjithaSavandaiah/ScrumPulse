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
        "Teams" => @"
            CREATE TABLE IF NOT EXISTS ""Teams"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Teams"" PRIMARY KEY,
                ""Name"" TEXT NOT NULL,
                ""Slug"" TEXT NOT NULL,
                ""Description"" TEXT NOT NULL DEFAULT '',
                ""JoinCode"" TEXT NOT NULL,
                ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Teams_Slug"" ON ""Teams"" (""Slug"");
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Teams_JoinCode"" ON ""Teams"" (""JoinCode"");
            CREATE INDEX IF NOT EXISTS ""IX_Teams_IsActive"" ON ""Teams"" (""IsActive"");
        ",
        "TeamLeaves" => @"
            CREATE TABLE IF NOT EXISTS ""TeamLeaves"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_TeamLeaves"" PRIMARY KEY,
                ""TeamMemberId"" TEXT NOT NULL,
                ""StartDate"" TEXT NOT NULL,
                ""EndDate"" TEXT NOT NULL,
                ""Reason"" TEXT NOT NULL DEFAULT '',
                ""LeaveType"" INTEGER NOT NULL DEFAULT 0,
                ""LeaveSlot"" INTEGER NOT NULL DEFAULT 0,
                ""Location"" TEXT NOT NULL DEFAULT 'Offshore',
                ""IsApproved"" INTEGER NOT NULL DEFAULT 1,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_TeamLeaves_TeamMemberId"" ON ""TeamLeaves"" (""TeamMemberId"");
            CREATE INDEX IF NOT EXISTS ""IX_TeamLeaves_IsApproved"" ON ""TeamLeaves"" (""IsApproved"");
        ",
        "PullRequestReviewLogs" => @"
            CREATE TABLE IF NOT EXISTS ""PullRequestReviewLogs"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_PullRequestReviewLogs"" PRIMARY KEY,
                ""WorkItemId"" TEXT NULL,
                ""AuthorId"" TEXT NOT NULL,
                ""ReviewerId"" TEXT NULL,
                ""SprintId"" TEXT NULL,
                ""PrNumber"" TEXT NOT NULL,
                ""PrTitle"" TEXT NOT NULL,
                ""PrUrl"" TEXT NOT NULL DEFAULT '',
                ""TotalCommentsCount"" INTEGER NOT NULL DEFAULT 0,
                ""ActionableCommentsCount"" INTEGER NOT NULL DEFAULT 0,
                ""ReviewSummary"" TEXT NOT NULL DEFAULT '',
                ""ReviewStatus"" INTEGER NOT NULL DEFAULT 0,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""MergedAtUtc"" TEXT NULL,
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "Sprints" => @"
            CREATE TABLE IF NOT EXISTS ""Sprints"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Sprints"" PRIMARY KEY,
                ""Name"" TEXT NOT NULL,
                ""Goal"" TEXT NOT NULL DEFAULT '',
                ""StartDate"" TEXT NOT NULL,
                ""EndDate"" TEXT NOT NULL,
                ""IsActive"" INTEGER NOT NULL DEFAULT 0,
                ""CommittedStoryPoints"" INTEGER NOT NULL DEFAULT 0,
                ""ConfidenceScore"" INTEGER NOT NULL DEFAULT 0,
                ""DailyHoursPerDeveloper"" REAL NOT NULL DEFAULT 8.5,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "TeamMembers" => @"
            CREATE TABLE IF NOT EXISTS ""TeamMembers"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_TeamMembers"" PRIMARY KEY,
                ""Name"" TEXT NOT NULL,
                ""Email"" TEXT NOT NULL,
                ""Role"" INTEGER NOT NULL DEFAULT 0,
                ""Location"" TEXT NOT NULL DEFAULT 'Offshore',
                ""Avatar"" TEXT NOT NULL DEFAULT '',
                ""ActiveWipLimit"" INTEGER NOT NULL DEFAULT 3,
                ""FocusArea"" TEXT NOT NULL DEFAULT '',
                ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                ""WeeklyWorkingDays"" INTEGER NOT NULL DEFAULT 5,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "WorkItems" => @"
            CREATE TABLE IF NOT EXISTS ""WorkItems"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_WorkItems"" PRIMARY KEY,
                ""Key"" TEXT NOT NULL,
                ""Title"" TEXT NOT NULL,
                ""Description"" TEXT NOT NULL DEFAULT '',
                ""Type"" INTEGER NOT NULL DEFAULT 0,
                ""Status"" INTEGER NOT NULL DEFAULT 0,
                ""Priority"" INTEGER NOT NULL DEFAULT 0,
                ""StoryPoints"" INTEGER NOT NULL DEFAULT 0,
                ""EstimatedHours"" REAL NULL,
                ""AssigneeId"" TEXT NULL,
                ""SprintId"" TEXT NULL,
                ""PrReviewerId"" TEXT NULL,
                ""DorAcceptanceCriteriaDefined"" INTEGER NOT NULL DEFAULT 1,
                ""DorDependenciesIdentified"" INTEGER NOT NULL DEFAULT 1,
                ""DorWireframeAvailable"" INTEGER NOT NULL DEFAULT 1,
                ""DodUnitTestsPassed"" INTEGER NOT NULL DEFAULT 0,
                ""DodPeerReviewCompleted"" INTEGER NOT NULL DEFAULT 0,
                ""DodMergedToMaster"" INTEGER NOT NULL DEFAULT 0,
                ""DodStagingVerified"" INTEGER NOT NULL DEFAULT 0,
                ""IsEscapedDefect"" INTEGER NOT NULL DEFAULT 0,
                ""PrUrl"" TEXT NOT NULL DEFAULT '',
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "Blockers" => @"
            CREATE TABLE IF NOT EXISTS ""Blockers"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Blockers"" PRIMARY KEY,
                ""Title"" TEXT NOT NULL,
                ""Description"" TEXT NOT NULL DEFAULT '',
                ""Category"" INTEGER NOT NULL DEFAULT 0,
                ""Severity"" INTEGER NOT NULL DEFAULT 0,
                ""Impact"" INTEGER NOT NULL DEFAULT 0,
                ""SprintId"" TEXT NULL,
                ""WorkItemId"" TEXT NULL,
                ""RaisedById"" TEXT NULL,
                ""RaisedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""ResolvedAtUtc"" TEXT NULL,
                ""ResolutionNotes"" TEXT NOT NULL DEFAULT '',
                ""SlaTargetHours"" REAL NOT NULL DEFAULT 24,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "DailyStandups" => @"
            CREATE TABLE IF NOT EXISTS ""DailyStandups"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_DailyStandups"" PRIMARY KEY,
                ""TeamMemberId"" TEXT NOT NULL,
                ""SprintId"" TEXT NULL,
                ""StandupDate"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""YesterdaySummary"" TEXT NOT NULL DEFAULT '',
                ""TodayPlan"" TEXT NOT NULL DEFAULT '',
                ""BlockersText"" TEXT NOT NULL DEFAULT '',
                ""MoodScore"" INTEGER NOT NULL DEFAULT 3,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "Monthly1on1Feedbacks" => @"
            CREATE TABLE IF NOT EXISTS ""Monthly1on1Feedbacks"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Monthly1on1Feedbacks"" PRIMARY KEY,
                ""TeamMemberId"" TEXT NOT NULL,
                ""MonthYear"" TEXT NOT NULL,
                ""ScrumMasterFeedback"" TEXT NOT NULL DEFAULT '',
                ""CdlFeedback"" TEXT NOT NULL DEFAULT '',
                ""ClientFeedback"" TEXT NOT NULL DEFAULT '',
                ""SelfReflection"" TEXT NOT NULL DEFAULT '',
                ""SmRating"" INTEGER NOT NULL DEFAULT 3,
                ""HappinessIndex"" INTEGER NOT NULL DEFAULT 3,
                ""ActionItems"" TEXT NOT NULL DEFAULT '',
                ""NextMonthGoals"" TEXT NOT NULL DEFAULT '',
                ""AiSynthesizedStrengths"" TEXT NOT NULL DEFAULT '',
                ""AiGrowthRecommendations"" TEXT NOT NULL DEFAULT '',
                ""AiBurnoutRiskAssessment"" TEXT NOT NULL DEFAULT '',
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "RetroCards" => @"
            CREATE TABLE IF NOT EXISTS ""RetroCards"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_RetroCards"" PRIMARY KEY,
                ""SprintId"" TEXT NOT NULL,
                ""Category"" INTEGER NOT NULL DEFAULT 0,
                ""Content"" TEXT NOT NULL DEFAULT '',
                ""AuthorId"" TEXT NULL,
                ""IsAnonymous"" INTEGER NOT NULL DEFAULT 0,
                ""UpvotesCount"" INTEGER NOT NULL DEFAULT 0,
                ""UpvoterMemberIdsJson"" TEXT NOT NULL DEFAULT '[]',
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "RetroActionItems" => @"
            CREATE TABLE IF NOT EXISTS ""RetroActionItems"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_RetroActionItems"" PRIMARY KEY,
                ""SprintId"" TEXT NOT NULL,
                ""Title"" TEXT NOT NULL,
                ""AssigneeId"" TEXT NULL,
                ""DueDate"" TEXT NULL,
                ""IsCompleted"" INTEGER NOT NULL DEFAULT 0,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "KudosCards" => @"
            CREATE TABLE IF NOT EXISTS ""KudosCards"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_KudosCards"" PRIMARY KEY,
                ""SenderId"" TEXT NOT NULL,
                ""ReceiverId"" TEXT NOT NULL,
                ""Badge"" TEXT NOT NULL,
                ""Message"" TEXT NOT NULL DEFAULT '',
                ""ReactionEmojisJson"" TEXT NOT NULL DEFAULT '{}',
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "TechDebtItems" => @"
            CREATE TABLE IF NOT EXISTS ""TechDebtItems"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_TechDebtItems"" PRIMARY KEY,
                ""Title"" TEXT NOT NULL,
                ""Description"" TEXT NOT NULL DEFAULT '',
                ""Severity"" INTEGER NOT NULL DEFAULT 0,
                ""Status"" INTEGER NOT NULL DEFAULT 0,
                ""EstimatedEffortHours"" REAL NOT NULL DEFAULT 0,
                ""Component"" TEXT NOT NULL DEFAULT '',
                ""SprintId"" TEXT NULL,
                ""WorkItemId"" TEXT NULL,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
                ""UpdatedAtUtc"" TEXT NULL,
                ""CreatedBy"" TEXT NULL,
                ""UpdatedBy"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""RowVersion"" BLOB NULL
            );
        ",
        "TechTalkLogs" => @"
            CREATE TABLE IF NOT EXISTS ""TechTalkLogs"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_TechTalkLogs"" PRIMARY KEY,
                ""Topic"" TEXT NOT NULL,
                ""SpeakerId"" TEXT NOT NULL,
                ""ScheduledDate"" TEXT NOT NULL,
                ""DurationMinutes"" INTEGER NOT NULL DEFAULT 60,
                ""SlidesUrl"" TEXT NOT NULL DEFAULT '',
                ""RecordingUrl"" TEXT NOT NULL DEFAULT '',
                ""FeedbackSummary"" TEXT NOT NULL DEFAULT '',
                ""AverageRating"" REAL NOT NULL DEFAULT 0,
                ""TeamId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL DEFAULT '1970-01-01 00:00:00',
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
