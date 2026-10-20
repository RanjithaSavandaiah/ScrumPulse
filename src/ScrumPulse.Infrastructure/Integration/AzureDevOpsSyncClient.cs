namespace ScrumPulse.Infrastructure.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.Integration;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Reads work items and their state history from Azure DevOps.
/// </summary>
/// <remarks>
/// Three calls per sync: WIQL for ids, a batch call for fields, and the updates endpoint per
/// item for the state transitions the stage timestamps come from. That last one is why this
/// integration exists, and it is also the expensive part, so it is capped.
/// </remarks>
public sealed class AzureDevOpsSyncClient : IBoardSyncClient
{
    public const string HttpClientName = "azure-devops";

    // Pinned: Azure DevOps changes response shapes between versions, and "latest" would let a
    // server-side upgrade silently break the field names read below.
    private const string ApiVersion = "7.1";

    /// <summary>Azure DevOps rejects a batch of more than 200 ids.</summary>
    private const int MaxBatchSize = 200;

    // Beyond this a sync outlives the patience of whoever triggered it.
    private const int MaxHistoryLookups = 150;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ILogger<AzureDevOpsSyncClient> _logger;

    public AzureDevOpsSyncClient(
        HttpClient httpClient,
        ICredentialProtector credentialProtector,
        ILogger<AzureDevOpsSyncClient> logger)
    {
        _httpClient = httpClient;
        _credentialProtector = credentialProtector;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> TestConnectionAsync(BoardConnection connection, CancellationToken ct = default)
    {
        var token = ResolveCredential(connection);
        if (token is null)
        {
            return "No access token is stored for this board. Enter a personal access token and save.";
        }

        try
        {
            // Cheapest authenticated call; a 200 proves the token works against this org.
            using var request = CreateRequest(
                HttpMethod.Get,
                $"{NormalizeOrganizationUrl(connection.OrganizationUrl)}/_apis/projects?api-version={ApiVersion}",
                token);

            using var response = await _httpClient.SendAsync(request, ct);

            return response.StatusCode switch
            {
                HttpStatusCode.OK => null,
                HttpStatusCode.Unauthorized or HttpStatusCode.NonAuthoritativeInformation =>
                    "Azure DevOps rejected the token. Check it has not expired and grants Work Items (Read).",
                HttpStatusCode.Forbidden =>
                    "The token is valid but lacks permission to read this organisation.",
                HttpStatusCode.NotFound =>
                    "That organisation URL was not found. It should look like https://dev.azure.com/your-org.",
                _ => $"Azure DevOps returned {(int)response.StatusCode} when testing the connection."
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Azure DevOps connection test failed.");
            return "Could not reach Azure DevOps. Check the organisation URL and your network.";
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportedWorkItem>> FetchWorkItemsAsync(
        BoardConnection connection,
        IReadOnlyCollection<string> rosterIdentities,
        DateTime? changedSince,
        CancellationToken ct = default)
    {
        var token = ResolveCredential(connection);
        if (token is null)
        {
            return [];
        }

        var organizationUrl = NormalizeOrganizationUrl(connection.OrganizationUrl);
        var mapping = BoardStateMapping.FromJson(connection.StateMappingJson);

        var workItemIds = await QueryWorkItemIdsAsync(
            organizationUrl, connection, token, rosterIdentities, changedSince, ct);
        if (workItemIds.Count == 0)
        {
            return [];
        }

        var imported = new List<ImportedWorkItem>(workItemIds.Count);
        var historyBudget = MaxHistoryLookups;

        foreach (var idBatch in workItemIds.Chunk(MaxBatchSize))
        {
            var fieldsById = await FetchFieldsAsync(organizationUrl, idBatch, token, ct);

            foreach (var (workItemId, fields) in fieldsById)
            {
                // Once the budget is spent the item still imports; it just carries no timeline,
                // which reads downstream as "not measured" rather than zero latency.
                var timeline = historyBudget-- > 0
                    ? await BuildTimelineAsync(organizationUrl, workItemId, token, mapping, ct)
                    : new StageTimeline();

                imported.Add(MapToImportedWorkItem(connection, workItemId, fields, timeline, mapping));
            }
        }

        _logger.LogInformation(
            "Azure DevOps sync fetched {ItemCount} work item(s) for project {Project}",
            imported.Count,
            connection.ProjectName);

        return imported;
    }

    /// <summary>Runs a WIQL query and returns the matching work item ids.</summary>
    private async Task<IReadOnlyList<int>> QueryWorkItemIdsAsync(
        string organizationUrl,
        BoardConnection connection,
        string token,
        IReadOnlyCollection<string> rosterIdentities,
        DateTime? changedSince,
        CancellationToken ct)
    {
        var conditions = new List<string>
        {
            $"[System.TeamProject] = '{EscapeWiqlLiteral(connection.ProjectName)}'"
        };

        if (!string.IsNullOrWhiteSpace(connection.TeamOrAreaPath))
        {
            conditions.Add($"[System.AreaPath] UNDER '{EscapeWiqlLiteral(connection.TeamOrAreaPath)}'");
        }

        if (changedSince is { } since)
        {
            conditions.Add($"[System.ChangedDate] >= '{since:yyyy-MM-dd}'");
        }

        // Restricting to the squad's own people separates their delivery from everyone else's
        // on a shared client project, and avoids pulling records about people who never agreed
        // to be tracked here. An empty roster skips the filter because Azure DevOps rejects an
        // empty IN clause; the sync service catches that case earlier.
        if (connection.AssigneeFilter == BoardAssigneeFilter.RosterMembersOnly &&
            rosterIdentities.Count > 0)
        {
            var identityList = string.Join(
                ", ",
                rosterIdentities
                    .Where(identity => !string.IsNullOrWhiteSpace(identity))
                    .Select(identity => $"'{EscapeWiqlLiteral(identity.Trim())}'"));

            if (!string.IsNullOrEmpty(identityList))
            {
                conditions.Add($"[System.AssignedTo] IN ({identityList})");
            }
        }

        var wiql = $"SELECT [System.Id] FROM WorkItems WHERE {string.Join(" AND ", conditions)} " +
                   "ORDER BY [System.ChangedDate] DESC";

        using var request = CreateRequest(
            HttpMethod.Post,
            $"{organizationUrl}/{Uri.EscapeDataString(connection.ProjectName)}/_apis/wit/wiql?api-version={ApiVersion}",
            token);

        request.Content = JsonContent.Create(new { query = wiql });

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            // The body can echo the query and account detail, so only the status is logged.
            _logger.LogWarning(
                "Azure DevOps WIQL query returned {StatusCode}.",
                (int)response.StatusCode);

            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<WiqlResponse>(SerializerOptions, ct);

        return payload?.WorkItems?.Select(reference => reference.Id).ToList() ?? [];
    }

    /// <summary>Fetches the fields for a batch of work item ids.</summary>
    private async Task<IReadOnlyList<(int Id, Dictionary<string, JsonElement> Fields)>> FetchFieldsAsync(
        string organizationUrl,
        IReadOnlyList<int> workItemIds,
        string token,
        CancellationToken ct)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"{organizationUrl}/_apis/wit/workitemsbatch?api-version={ApiVersion}",
            token);

        // System.Description is never requested: it is where pasted credentials and customer
        // records actually turn up, and no metric reads it.
        request.Content = JsonContent.Create(new
        {
            ids = workItemIds,
            fields = new[]
            {
                "System.Id",
                "System.Title",
                "System.WorkItemType",
                "System.State",
                "System.AssignedTo",
                "System.IterationPath",
                "System.CreatedDate",
                "Microsoft.VSTS.Common.Priority",
                "Microsoft.VSTS.Scheduling.StoryPoints"
            }
        });

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Azure DevOps work item batch returned {StatusCode}.",
                (int)response.StatusCode);

            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<WorkItemBatchResponse>(SerializerOptions, ct);

        return payload?.Value?
            .Where(item => item.Fields is not null)
            .Select(item => (item.Id, item.Fields!))
            .ToList() ?? [];
    }

    /// <summary>
    /// Reads an item's state transitions and derives its stage timestamps. Uses the updates
    /// endpoint, which returns changed fields only, rather than revisions, which returns a full
    /// copy of the item per revision.
    /// </summary>
    private async Task<StageTimeline> BuildTimelineAsync(
        string organizationUrl,
        int workItemId,
        string token,
        BoardStateMapping mapping,
        CancellationToken ct)
    {
        try
        {
            using var request = CreateRequest(
                HttpMethod.Get,
                $"{organizationUrl}/_apis/wit/workItems/{workItemId}/updates?api-version={ApiVersion}",
                token);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new StageTimeline();
            }

            var payload = await response.Content.ReadFromJsonAsync<WorkItemUpdatesResponse>(SerializerOptions, ct);

            var stateChanges = payload?.Value?
                .Where(update => update.Fields is not null)
                .Select(update => ReadStateChange(update))
                .Where(change => change is not null)
                .Select(change => change!.Value)
                .ToList() ?? [];

            return StageTimelineBuilder.Build(stateChanges, mapping);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // One item's missing history must not abort the whole sync.
            _logger.LogDebug(exception, "Could not read history for work item {WorkItemId}.", workItemId);
            return new StageTimeline();
        }
    }

    private static BoardStateChange? ReadStateChange(WorkItemUpdate update)
    {
        if (update.Fields is null ||
            !update.Fields.TryGetValue("System.State", out var stateChange) ||
            stateChange.NewValue is null)
        {
            return null;
        }

        // RevisedDate covers older items whose early revisions predate ChangedDate.
        var changedAt = ReadDate(update.Fields, "System.ChangedDate") ?? update.RevisedDate;

        return changedAt is null
            ? null
            : new BoardStateChange(stateChange.NewValue.ToString() ?? string.Empty, changedAt.Value);
    }

    private static DateTime? ReadDate(Dictionary<string, FieldChange> fields, string fieldName) =>
        fields.TryGetValue(fieldName, out var change) &&
        change.NewValue is not null &&
        DateTime.TryParse(change.NewValue.ToString(), out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    private static ImportedWorkItem MapToImportedWorkItem(
        BoardConnection connection,
        int workItemId,
        Dictionary<string, JsonElement> fields,
        StageTimeline timeline,
        BoardStateMapping mapping)
    {
        var boardState = ReadString(fields, "System.State");

        return new ImportedWorkItem
        {
            ExternalId = workItemId.ToString(),
            Key = $"AB#{workItemId}",
            // Free text passes through the connection's content policy rather than being copied.
            Title = ResolveTitle(connection, workItemId, fields),
            Description = string.Empty,
            ExternalUrl =
                $"{NormalizeOrganizationUrl(connection.OrganizationUrl)}/" +
                $"{Uri.EscapeDataString(connection.ProjectName)}/_workitems/edit/{workItemId}",
            Type = MapWorkItemType(ReadString(fields, "System.WorkItemType")),

            // Unmapped states display as Backlog, but the raw name travels alongside so the
            // sync can report the gap rather than hide it.
            Status = mapping.Resolve(boardState) ?? WorkItemStatus.Backlog,
            BoardState = boardState,

            Priority = MapPriority(ReadInt(fields, "Microsoft.VSTS.Common.Priority")),
            StoryPoints = (int)Math.Round(ReadDouble(fields, "Microsoft.VSTS.Scheduling.StoryPoints") ?? 0),
            AssignedToDisplayName = ReadAssignedTo(fields),
            IterationPath = ReadString(fields, "System.IterationPath"),
            CreatedAtUtc = ReadDateField(fields, "System.CreatedDate") ?? DateTime.UtcNow,
            Timeline = timeline
        };
    }

    /// <summary>
    /// Returns the title permitted by the connection's content policy. The default keeps the
    /// board reference only, which every delivery metric can work from and which keeps a
    /// client's wording out of this database.
    /// </summary>
    private static string ResolveTitle(
        BoardConnection connection,
        int workItemId,
        Dictionary<string, JsonElement> fields)
    {
        var boardReference = $"AB#{workItemId}";

        if (connection.ContentPolicy == ContentSensitivityPolicy.MetricsOnly)
        {
            return boardReference;
        }

        var redactedTitle = SensitiveContentRedactor.Redact(ReadString(fields, "System.Title"));

        return string.IsNullOrWhiteSpace(redactedTitle)
            ? boardReference
            : $"{boardReference} {redactedTitle}";
    }

    // Azure DevOps omits empty fields rather than returning null, so every read tolerates absence.

    private static string? ReadString(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;

    private static double? ReadDouble(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.TryGetDouble(out var parsed) ? parsed : null;

    private static DateTime? ReadDateField(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.TryGetDateTime(out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    // AssignedTo is an identity object rather than a string, and absent on unassigned items.
    private static string? ReadAssignedTo(Dictionary<string, JsonElement> fields) =>
        fields.TryGetValue("System.AssignedTo", out var value) &&
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty("displayName", out var displayName)
            ? displayName.GetString()
            : null;

    private static WorkItemType MapWorkItemType(string? azureDevOpsType) => azureDevOpsType switch
    {
        "Bug" => WorkItemType.Bug,
        "Task" => WorkItemType.TaskPbi,
        "User Story" or "Product Backlog Item" or "Requirement" => WorkItemType.UserStory,
        _ => WorkItemType.TaskPbi
    };

    /// <summary>Azure DevOps priority runs 1 as most urgent.</summary>
    private static PriorityLevel MapPriority(int? azureDevOpsPriority) => azureDevOpsPriority switch
    {
        1 => PriorityLevel.Critical,
        2 => PriorityLevel.High,
        3 => PriorityLevel.Medium,
        4 => PriorityLevel.Low,
        _ => PriorityLevel.Medium
    };

    private string? ResolveCredential(BoardConnection connection) =>
        string.IsNullOrWhiteSpace(connection.EncryptedCredential)
            ? null
            : _credentialProtector.Unprotect(connection.EncryptedCredential);

    /// <summary>
    /// Basic auth with an empty username and the token as password, set per request because
    /// the shared HttpClient is reused across squads and a sticky header would send one
    /// squad's token to another's organisation.
    /// </summary>
    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);

        var basicCredential = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicCredential);

        return request;
    }

    private static string NormalizeOrganizationUrl(string organizationUrl) =>
        organizationUrl.TrimEnd('/');

    /// <summary>
    /// Project and area path are configured values, so an unescaped quote would break out of
    /// the literal and alter the query. WIQL escapes a quote by doubling it.
    /// </summary>
    private static string EscapeWiqlLiteral(string value) => value.Replace("'", "''");

    private sealed class WiqlResponse
    {
        [JsonPropertyName("workItems")]
        public List<WorkItemReference>? WorkItems { get; set; }
    }

    private sealed class WorkItemReference
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private sealed class WorkItemBatchResponse
    {
        [JsonPropertyName("value")]
        public List<WorkItemFields>? Value { get; set; }
    }

    private sealed class WorkItemFields
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("fields")]
        public Dictionary<string, JsonElement>? Fields { get; set; }
    }

    private sealed class WorkItemUpdatesResponse
    {
        [JsonPropertyName("value")]
        public List<WorkItemUpdate>? Value { get; set; }
    }

    private sealed class WorkItemUpdate
    {
        [JsonPropertyName("fields")]
        public Dictionary<string, FieldChange>? Fields { get; set; }

        [JsonPropertyName("revisedDate")]
        public DateTime? RevisedDate { get; set; }
    }

    private sealed class FieldChange
    {
        [JsonPropertyName("oldValue")]
        public object? OldValue { get; set; }

        [JsonPropertyName("newValue")]
        public object? NewValue { get; set; }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportedPullRequest>> FetchPullRequestsAsync(
        BoardConnection connection,
        IReadOnlyCollection<string> rosterIdentities,
        CancellationToken ct = default)
    {
        var token = ResolveCredential(connection);
        if (token is null)
        {
            return [];
        }

        var organizationUrl = NormalizeOrganizationUrl(connection.OrganizationUrl);
        var project = Uri.EscapeDataString(connection.ProjectName);

        using var reposRequest = CreateRequest(
            HttpMethod.Get,
            $"{organizationUrl}/{project}/_apis/git/repositories?api-version={ApiVersion}",
            token);

        using var reposResponse = await _httpClient.SendAsync(reposRequest, ct);
        if (!reposResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Azure DevOps Git repositories query returned {StatusCode} for project {Project}.",
                (int)reposResponse.StatusCode,
                connection.ProjectName);
            return [];
        }

        var reposPayload = await reposResponse.Content.ReadFromJsonAsync<GitRepositoriesResponse>(SerializerOptions, ct);
        var repositories = reposPayload?.Value ?? [];
        if (repositories.Count == 0)
        {
            return [];
        }

        var imported = new List<ImportedPullRequest>();
        var threadLookupBudget = 30;

        foreach (var repo in repositories.Take(5))
        {
            using var prRequest = CreateRequest(
                HttpMethod.Get,
                $"{organizationUrl}/{project}/_apis/git/repositories/{repo.Id}/pullrequests?searchCriteria.status=all&$top=50&api-version={ApiVersion}",
                token);

            using var prResponse = await _httpClient.SendAsync(prRequest, ct);
            if (!prResponse.IsSuccessStatusCode)
            {
                continue;
            }

            var prPayload = await prResponse.Content.ReadFromJsonAsync<GitPullRequestsResponse>(SerializerOptions, ct);
            var pullRequests = prPayload?.Value ?? [];

            foreach (var pr in pullRequests)
            {
                var authorEmail = pr.CreatedBy?.UniqueName;
                var authorName = pr.CreatedBy?.DisplayName;

                if (connection.AssigneeFilter == BoardAssigneeFilter.RosterMembersOnly && rosterIdentities.Count > 0)
                {
                    var isRosterAuthor = rosterIdentities.Any(id =>
                        (!string.IsNullOrWhiteSpace(authorEmail) && id.Equals(authorEmail, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(authorName) && id.Equals(authorName, StringComparison.OrdinalIgnoreCase)));

                    if (!isRosterAuthor)
                    {
                        continue;
                    }
                }

                var comments = new List<ReviewComment>();
                if (threadLookupBudget-- > 0)
                {
                    try
                    {
                        using var threadsRequest = CreateRequest(
                            HttpMethod.Get,
                            $"{organizationUrl}/{project}/_apis/git/repositories/{repo.Id}/pullrequests/{pr.PullRequestId}/threads?api-version={ApiVersion}",
                            token);

                        using var threadsResponse = await _httpClient.SendAsync(threadsRequest, ct);
                        if (threadsResponse.IsSuccessStatusCode)
                        {
                            var threadsPayload = await threadsResponse.Content.ReadFromJsonAsync<GitThreadsResponse>(SerializerOptions, ct);
                            if (threadsPayload?.Value != null)
                            {
                                foreach (var thread in threadsPayload.Value)
                                {
                                    if (thread.Comments == null) continue;
                                    foreach (var c in thread.Comments)
                                    {
                                        if (string.IsNullOrWhiteSpace(c.Content) || c.CommentType == "system") continue;
                                        var isFromRoster = rosterIdentities.Count == 0 || rosterIdentities.Any(id =>
                                            (!string.IsNullOrWhiteSpace(c.Author?.UniqueName) && id.Equals(c.Author.UniqueName, StringComparison.OrdinalIgnoreCase)) ||
                                            (!string.IsNullOrWhiteSpace(c.Author?.DisplayName) && id.Equals(c.Author.DisplayName, StringComparison.OrdinalIgnoreCase)));
                                        comments.Add(new ReviewComment(pr.PullRequestId, c.Content, isFromRoster));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Failed to fetch review threads for PR #{PrId}", pr.PullRequestId);
                    }
                }

                var report = ReviewCommentAnalyser.Analyse(comments);
                var summary = report.CoachingSignals.Count > 0
                    ? string.Join("; ", report.CoachingSignals)
                    : (report.Themes.Count > 0
                        ? $"Themes: {string.Join(", ", report.Themes.Select(t => t.Theme))}"
                        : "No review comments recorded");

                imported.Add(new ImportedPullRequest(
                    ExternalPrId: pr.PullRequestId.ToString(),
                    PrNumber: $"#{pr.PullRequestId}",
                    Title: pr.Title ?? $"Pull Request #{pr.PullRequestId}",
                    Url: pr.Url ?? string.Empty,
                    AuthorIdentity: authorEmail,
                    AuthorDisplayName: authorName,
                    ReviewerIdentity: pr.Reviewers?.FirstOrDefault()?.UniqueName,
                    ReviewerDisplayName: pr.Reviewers?.FirstOrDefault()?.DisplayName,
                    CreatedAtUtc: pr.CreationDate.ToUniversalTime(),
                    ClosedAtUtc: pr.ClosedDate?.ToUniversalTime(),
                    Status: pr.Status ?? "active",
                    TotalCommentsCount: report.TotalComments,
                    ActionableCommentsCount: report.ActionableComments,
                    ReviewSummary: summary,
                    Comments: comments
                ));
            }
        }

        _logger.LogInformation(
            "Azure DevOps sync fetched {Count} pull request(s) for project {Project}",
            imported.Count,
            connection.ProjectName);

        return imported;
    }

    private sealed class GitRepositoriesResponse
    {
        [JsonPropertyName("value")]
        public List<GitRepositoryItem>? Value { get; set; }
    }

    private sealed class GitRepositoryItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class GitPullRequestsResponse
    {
        [JsonPropertyName("value")]
        public List<GitPullRequestItem>? Value { get; set; }
    }

    private sealed class GitPullRequestItem
    {
        [JsonPropertyName("pullRequestId")]
        public int PullRequestId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("creationDate")]
        public DateTime CreationDate { get; set; }

        [JsonPropertyName("closedDate")]
        public DateTime? ClosedDate { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("createdBy")]
        public GitIdentity? CreatedBy { get; set; }

        [JsonPropertyName("reviewers")]
        public List<GitReviewer>? Reviewers { get; set; }
    }

    private sealed class GitIdentity
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("uniqueName")]
        public string? UniqueName { get; set; }
    }

    private sealed class GitReviewer
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("uniqueName")]
        public string? UniqueName { get; set; }

        [JsonPropertyName("vote")]
        public int Vote { get; set; }
    }

    private sealed class GitThreadsResponse
    {
        [JsonPropertyName("value")]
        public List<GitThreadItem>? Value { get; set; }
    }

    private sealed class GitThreadItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("comments")]
        public List<GitCommentItem>? Comments { get; set; }
    }

    private sealed class GitCommentItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("commentType")]
        public string? CommentType { get; set; }

        [JsonPropertyName("author")]
        public GitIdentity? Author { get; set; }
    }
}
