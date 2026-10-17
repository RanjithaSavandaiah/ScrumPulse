namespace ScrumPulse.Tests.AI;

using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Evaluation;
using ScrumPulse.AI.Providers;
using ScrumPulse.AI.Retrieval;
using ScrumPulse.AI.Services;
using ScrumPulse.AI.Strategies;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;

/// <summary>
/// Builds a <see cref="MicrosoftAgentService"/> for tests without reaching a model provider.
/// </summary>
/// <remarks>
/// Tests must never make a real inference call: it would need an API key, spend the daily
/// free-tier allowance, and return different text on every run. The harness therefore
/// supplies an <see cref="IAgentProvider"/> that reports no inference available, which sends
/// the service down its deterministic path. That path is the one worth asserting on anyway,
/// because it is what production serves whenever the allowance is gone.
/// </remarks>
internal static class AiTestHarness
{
    /// <summary>Creates the service wired to the supplied context, with inference disabled.</summary>
    internal static MicrosoftAgentService CreateService(
        AppDbContext dbContext,
        IIdempotencyStore? responseCache = null,
        AgentConfiguration? configuration = null)
    {
        var agentConfiguration = configuration ?? new AgentConfiguration();

        return new MicrosoftAgentService(
            new DeliveryMetricsRetriever(dbContext),
            responseCache ?? new MemoryIdempotencyStore(),
            new OfflineAgentProvider(),
            new DailyRequestBudget(dailyLimit: 0, TimeProvider.System, NullLogger<DailyRequestBudget>.Instance),
            agentConfiguration,
            new AiResponseEvaluator(),
            [
                new IndividualInsightGenerator(),
                new SprintInsightGenerator(),
                new CompanyInsightGenerator()
            ],
            NullLogger<MicrosoftAgentService>.Instance);
    }

    /// <summary>
    /// Stands in for a provider chain with no configured API key, which is how a deployment
    /// behaves before the operator sets GROQ_API_KEY or GEMINI_API_KEY.
    /// </summary>
    private sealed class OfflineAgentProvider : IAgentProvider
    {
        public bool IsLiveInferenceAvailable => false;

        public IReadOnlyList<ProvisionedAgent> BuildAgentChain(
            string instructions,
            string agentName,
            IReadOnlyList<Microsoft.Extensions.AI.AITool>? tools = null) => [];
    }
}
