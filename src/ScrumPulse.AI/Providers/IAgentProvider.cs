namespace ScrumPulse.AI.Providers;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

/// <summary>
/// Supplies ready-to-run agents, hiding which upstream provider is currently serving them.
/// </summary>
public interface IAgentProvider
{
    /// <summary>
    /// True when at least one provider has an API key configured. False means the deployment
    /// is running without inference and callers should expect the deterministic analysis.
    /// </summary>
    bool IsLiveInferenceAvailable { get; }

    /// <summary>
    /// Builds an agent with the supplied persona and optional tools, in priority order across
    /// the configured providers.
    /// </summary>
    /// <param name="instructions">System instructions defining the agent's persona and output contract.</param>
    /// <param name="agentName">Name recorded in telemetry for this agent.</param>
    /// <param name="tools">Functions the agent may call to retrieve data for itself.</param>
    /// <returns>Agents in the order they should be attempted, primary first.</returns>
    IReadOnlyList<ProvisionedAgent> BuildAgentChain(
        string instructions,
        string agentName,
        IReadOnlyList<AITool>? tools = null);
}

/// <summary>An agent together with the provider that backs it, so failures can name a culprit.</summary>
/// <param name="ProviderName">Provider serving this agent.</param>
/// <param name="Agent">The agent to run.</param>
public sealed record ProvisionedAgent(string ProviderName, AIAgent Agent);
