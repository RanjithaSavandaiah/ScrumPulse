namespace ScrumPulse.AI.Services;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ScrumPulse.AI.Agents;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Evaluation;
using ScrumPulse.AI.Prompt;
using ScrumPulse.AI.Providers;
using ScrumPulse.AI.Retrieval;
using ScrumPulse.AI.Strategies;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;

/// <summary>
/// Delivery coach built on the Microsoft Agent Framework.
/// </summary>
/// <remarks>
/// <para>
/// Every insight follows the same path: serve from cache when possible, otherwise retrieve
/// the squad's metrics, put them to an agent, score what comes back, and fall back to a
/// deterministic analysis if any step fails.
/// </para>
/// <para>
/// That fallback is load-bearing rather than decorative. This deployment runs on free model
/// tiers with a daily request cap, so the coach has to keep working once the cap is reached.
/// The <see cref="IInsightGenerator"/> implementations restate the same metrics in rule-based
/// form: less insightful, always available, never wrong.
/// </para>
/// <para>
/// An earlier version of this class built a prompt, logged its token count, then discarded it
/// and returned rule-based text that the UI presented as model output. Every path that claims
/// to be model-generated now calls a model, and the ones that fall back say so in the log.
/// </para>
/// </remarks>
public sealed class MicrosoftAgentService : IAiAgentService
{
    private static readonly JsonSerializerOptions InsightSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDeliveryMetricsRetriever _metricsRetriever;
    private readonly IIdempotencyStore _responseCache;
    private readonly IAgentProvider _agentProvider;
    private readonly DailyRequestBudget _requestBudget;
    private readonly AgentConfiguration _configuration;
    private readonly InsightPromptBuilder _promptBuilder;
    private readonly AiResponseEvaluator _evaluator;
    private readonly IReadOnlyDictionary<string, IInsightGenerator> _fallbackGenerators;
    private readonly ILogger<MicrosoftAgentService> _logger;

    public MicrosoftAgentService(
        IDeliveryMetricsRetriever metricsRetriever,
        IIdempotencyStore responseCache,
        IAgentProvider agentProvider,
        DailyRequestBudget requestBudget,
        AgentConfiguration configuration,
        AiResponseEvaluator evaluator,
        IEnumerable<IInsightGenerator> fallbackGenerators,
        ILogger<MicrosoftAgentService> logger)
    {
        _metricsRetriever = metricsRetriever;
        _responseCache = responseCache;
        _agentProvider = agentProvider;
        _requestBudget = requestBudget;
        _configuration = configuration;
        _promptBuilder = new InsightPromptBuilder(configuration);
        _evaluator = evaluator;
        _logger = logger;

        _fallbackGenerators = fallbackGenerators.ToDictionary(
            generator => generator.Level,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<AiSuggestionResponse> GenerateIndividualCoachingAsync(
        Guid memberId,
        CancellationToken ct = default) =>
        GenerateInsightAsync(
            cacheKey: $"ai:individual:{memberId}",
            level: InsightLevels.Individual,
            analysisScope: "one engineer's delivery and wellbeing signals",
            buildContext: cancellation => _metricsRetriever.BuildMemberContextAsync(memberId, cancellation),
            buildPrompt: _promptBuilder.BuildIndividualPrompt,
            ct);

    /// <inheritdoc />
    public Task<AiSuggestionResponse> GenerateProjectSprintInsightsAsync(
        Guid sprintId,
        CancellationToken ct = default) =>
        GenerateInsightAsync(
            cacheKey: $"ai:sprint:{sprintId}",
            level: InsightLevels.Project,
            analysisScope: "a single sprint in flight",
            buildContext: cancellation => _metricsRetriever.BuildSprintContextAsync(sprintId, cancellation),
            buildPrompt: _promptBuilder.BuildSprintPrompt,
            ct);

    /// <inheritdoc />
    public Task<AiSuggestionResponse> GenerateCompanyStrategicInsightsAsync(CancellationToken ct = default) =>
        GenerateInsightAsync(
            cacheKey: "ai:portfolio",
            level: InsightLevels.Company,
            analysisScope: "delivery trends across every sprint this squad has run",
            buildContext: _metricsRetriever.BuildPortfolioContextAsync,
            buildPrompt: _promptBuilder.BuildPortfolioPrompt,
            ct);

    /// <inheritdoc />
    public async Task<CopilotChatResponse> ProcessCopilotChatAsync(
        CopilotChatRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return new CopilotChatResponse(
                "Ask me about sprint risk, cycle times, blockers, or a squad member's delivery.",
                DefaultFollowUps(),
                DateTime.UtcNow);
        }

        if (!_agentProvider.IsLiveInferenceAvailable || !_requestBudget.TryReserve())
        {
            return new CopilotChatResponse(
                "The delivery assistant is unavailable right now, so I cannot answer that. " +
                "The sprint, blocker and capacity dashboards are still live and hold the same data.",
                DefaultFollowUps(),
                DateTime.UtcNow);
        }

        // Tools rather than a pre-built context block. The assistant decides which figures it
        // needs and fetches them itself, so a question about one member does not drag the
        // whole squad's metrics through the prompt. On a per-day request budget the saving is
        // in tokens; the accuracy gain is that it can follow up on its own findings.
        var tools = DeliveryDataTools.CreateFor(_metricsRetriever);

        var instructions = $"""
            You are {_configuration.Persona} embedded in a delivery tracking tool.

            Answer questions about this squad's sprint using the tools available to you.

            Rules:
            - Call a tool to obtain figures. Never state a metric you have not retrieved.
            - If the tools cannot answer, say so plainly and name what is missing.
            - Answer in at most four sentences. No preamble.
            """;

        var agentChain = _agentProvider.BuildAgentChain(instructions, "ScrumPulse delivery assistant", tools);

        foreach (var provisioned in agentChain)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await provisioned.Agent.RunAsync(request.Prompt, cancellationToken: ct);
                stopwatch.Stop();

                var answer = response.Text?.Trim();
                if (string.IsNullOrWhiteSpace(answer))
                {
                    continue;
                }

                _logger.LogInformation(
                    "Copilot answered via {Provider} in {ElapsedMs}ms",
                    provisioned.ProviderName,
                    stopwatch.ElapsedMilliseconds);

                return new CopilotChatResponse(answer, DefaultFollowUps(), DateTime.UtcNow);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                HandleProviderFailure(exception, provisioned.ProviderName, "copilot chat");
            }
        }

        return new CopilotChatResponse(
            "I could not reach the delivery assistant just now. Please try again shortly.",
            DefaultFollowUps(),
            DateTime.UtcNow);
    }

    /// <summary>Runs the shared cache, retrieve, generate, evaluate, fall back pipeline.</summary>
    private async Task<AiSuggestionResponse> GenerateInsightAsync(
        string cacheKey,
        string level,
        string analysisScope,
        Func<CancellationToken, Task<InsightContext>> buildContext,
        Func<InsightContext, string> buildPrompt,
        CancellationToken ct)
    {
        var cached = await _responseCache.GetResponseAsync<AiSuggestionResponse>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Serving cached {Level} insight for {CacheKey}", level, cacheKey);
            return cached;
        }

        var context = await buildContext(ct);

        var response = await TryGenerateWithAgentAsync(level, analysisScope, buildPrompt(context), ct)
            ?? GenerateDeterministicInsight(level, context);

        await _responseCache.SaveResponseAsync(cacheKey, response, _configuration.CacheTtl, ct);
        return response;
    }

    /// <summary>Attempts live generation across the provider chain.</summary>
    /// <returns>A generated insight, or null when generation was skipped, failed, or scored too low.</returns>
    private async Task<AiSuggestionResponse?> TryGenerateWithAgentAsync(
        string level,
        string analysisScope,
        string prompt,
        CancellationToken ct)
    {
        if (!_agentProvider.IsLiveInferenceAvailable)
        {
            return null;
        }

        if (!_requestBudget.TryReserve())
        {
            _logger.LogInformation(
                "Skipping model call for {Level} insight: the daily request budget is spent.",
                level);

            return null;
        }

        _logger.LogDebug(
            "{Level} prompt built: ~{PromptTokens} tokens against a {Budget} budget ({Headroom} reserved)",
            level,
            TokenBudget.Estimate(prompt),
            _configuration.EffectiveContextTokens,
            _configuration.HeadroomTokens);

        var instructions = _promptBuilder.BuildSystemInstructions(analysisScope);
        var agentChain = _agentProvider.BuildAgentChain(instructions, $"ScrumPulse {level} coach");

        foreach (var provisioned in agentChain)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                var agentResponse = await provisioned.Agent.RunAsync(
                    prompt,
                    options: new ChatClientAgentRunOptions(new ChatOptions
                    {
                        // JSON mode keeps the reply parseable. The schema itself is stated in
                        // the instructions because the two providers expose schema enforcement
                        // differently, and plain JSON mode is the subset both support.
                        ResponseFormat = ChatResponseFormat.Json
                    }),
                    cancellationToken: ct);

                stopwatch.Stop();

                var generated = ParseGeneratedInsight(agentResponse.Text);
                if (generated is null || !generated.IsUsable())
                {
                    _logger.LogWarning(
                        "{Provider} returned an unusable {Level} insight. Trying the next provider.",
                        provisioned.ProviderName,
                        level);

                    continue;
                }

                var response = ToSuggestionResponse(level, generated);

                if (_configuration.EnableEvaluation)
                {
                    var evaluation = _evaluator.Evaluate(response);

                    _logger.LogInformation(
                        "{Level} insight from {Provider} in {ElapsedMs}ms scored {Score}/100 ({Dimensions})",
                        level,
                        provisioned.ProviderName,
                        stopwatch.ElapsedMilliseconds,
                        evaluation.CompositeScore,
                        string.Join(", ", evaluation.DimensionScores.Select(d => $"{d.Key}={d.Value}")));

                    if (evaluation.CompositeScore < _configuration.MinQualityScore)
                    {
                        // A response below the bar is worse than no response: it reads as
                        // authoritative while being vague or ungrounded. The rule-based
                        // analysis at least states the real numbers.
                        _logger.LogWarning(
                            "Discarding {Level} insight from {Provider}: scored {Score}, below the {Minimum} bar.",
                            level,
                            provisioned.ProviderName,
                            evaluation.CompositeScore,
                            _configuration.MinQualityScore);

                        continue;
                    }
                }

                return response;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                HandleProviderFailure(exception, provisioned.ProviderName, $"{level} insight");
            }
        }

        _logger.LogWarning(
            "No provider produced an acceptable {Level} insight. Serving deterministic analysis.",
            level);

        return null;
    }

    /// <summary>Produces the rule-based analysis used whenever live generation is unavailable.</summary>
    private AiSuggestionResponse GenerateDeterministicInsight(string level, InsightContext context)
    {
        if (!_fallbackGenerators.TryGetValue(level, out var generator))
        {
            // Reaching here means a level was added without its fallback. Returning an honest
            // empty result beats throwing into a dashboard someone is waiting on.
            _logger.LogError("No deterministic generator is registered for level {Level}.", level);

            return new AiSuggestionResponse(
                level,
                "Analysis unavailable",
                "No analysis could be produced for this view.",
                [],
                [],
                "Unknown",
                DateTime.UtcNow);
        }

        return generator.Generate(context);
    }

    /// <summary>Deserialises model output into <see cref="GeneratedInsight"/>.</summary>
    /// <remarks>
    /// Models asked for JSON still occasionally wrap it in a markdown fence or put a sentence
    /// in front of it, so the outermost JSON object is extracted rather than assuming the
    /// whole reply parses.
    /// </remarks>
    private GeneratedInsight? ParseGeneratedInsight(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        var start = rawResponse.IndexOf('{');
        var end = rawResponse.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            _logger.LogWarning("Model response contained no JSON object.");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GeneratedInsight>(
                rawResponse[start..(end + 1)],
                InsightSerializerOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Model response was not valid JSON.");
            return null;
        }
    }

    private static AiSuggestionResponse ToSuggestionResponse(string level, GeneratedInsight generated) =>
        new(
            level,
            string.IsNullOrWhiteSpace(generated.Title) ? $"{level} delivery analysis" : generated.Title.Trim(),
            generated.Summary!.Trim(),
            [.. generated.KeyFindings!.Where(finding => !string.IsNullOrWhiteSpace(finding)).Select(finding => finding.Trim())],
            [.. generated.Recommendations!.Where(action => !string.IsNullOrWhiteSpace(action)).Select(action => action.Trim())],
            NormaliseRiskLevel(generated.RiskLevel),
            DateTime.UtcNow);

    /// <summary>Maps free-text risk wording onto the four levels the UI colours by.</summary>
    private static string NormaliseRiskLevel(string? riskLevel)
    {
        if (string.IsNullOrWhiteSpace(riskLevel))
        {
            return "Unknown";
        }

        var trimmed = riskLevel.Trim();

        return trimmed switch
        {
            _ when trimmed.Contains("critical", StringComparison.OrdinalIgnoreCase) => "Critical",
            _ when trimmed.Contains("high", StringComparison.OrdinalIgnoreCase) => "High",
            _ when trimmed.Contains("medium", StringComparison.OrdinalIgnoreCase) => "Medium",
            _ when trimmed.Contains("low", StringComparison.OrdinalIgnoreCase) => "Low",
            _ => "Unknown"
        };
    }

    /// <summary>Logs a provider failure, latching the quota breaker when the cause was rate limiting.</summary>
    private void HandleProviderFailure(Exception exception, string providerName, string operation)
    {
        if (IsQuotaFailure(exception))
        {
            _requestBudget.RecordQuotaExhausted();

            _logger.LogWarning(
                "{Provider} rejected the {Operation} call for quota reasons. Failing over.",
                providerName,
                operation);

            return;
        }

        _logger.LogError(
            exception,
            "{Provider} failed during {Operation}. Failing over to the next provider.",
            providerName,
            operation);
    }

    /// <summary>Decides whether a failure was rate limiting rather than a transient fault.</summary>
    /// <remarks>
    /// The OpenAI-compatible clients surface HTTP 429 as an exception carrying the status in
    /// its message, and the two providers word the body differently. Matching on the status
    /// code with a wording fallback is imprecise, but misreading a real 429 as transient means
    /// retrying into a wall, which is the worse mistake.
    /// </remarks>
    private static bool IsQuotaFailure(Exception exception)
    {
        var message = exception.Message;

        return message.Contains("429", StringComparison.Ordinal)
            || message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("quota", StringComparison.OrdinalIgnoreCase)
            || message.Contains("resource_exhausted", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> DefaultFollowUps() =>
    [
        "What is putting this sprint at risk?",
        "Which blockers have been open longest?",
        "How is our say-do ratio trending?"
    ];
}

/// <summary>Level identifiers shared by the service, the API, and the fallback generators.</summary>
public static class InsightLevels
{
    public const string Individual = "Individual";
    public const string Project = "Project";
    public const string Company = "Company";
}
