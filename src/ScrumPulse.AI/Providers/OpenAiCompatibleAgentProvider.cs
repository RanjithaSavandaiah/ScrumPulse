namespace ScrumPulse.AI.Providers;

using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using ScrumPulse.AI.Configuration;

/// <summary>
/// Builds Microsoft Agent Framework agents over any OpenAI-compatible inference endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Groq and Gemini both accept the OpenAI chat-completions dialect, so a single client type
/// reaches either. Providers are attempted in configuration order, which lets the fast free
/// provider serve normally and the second absorb its daily limit without a code change.
/// </para>
/// <para>
/// Chat clients are built once and reused. The underlying handler pools connections, so
/// constructing one per request would leak sockets under load.
/// </para>
/// </remarks>
public sealed class OpenAiCompatibleAgentProvider : IAgentProvider, IDisposable
{
    private readonly IReadOnlyList<ConfiguredProvider> _providers;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<OpenAiCompatibleAgentProvider> _logger;

    public OpenAiCompatibleAgentProvider(
        AgentConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<OpenAiCompatibleAgentProvider>();
        _providers = BuildConfiguredProviders(configuration, loggerFactory);

        if (_providers.Count == 0)
        {
            _logger.LogWarning(
                "No AI provider has an API key configured. The coach will serve its deterministic " +
                "analysis instead of model-generated insight. Set {Variables} to enable inference.",
                string.Join(" or ", configuration.Providers.Select(provider => provider.ApiKeyEnvironmentVariable)));
        }
        else
        {
            _logger.LogInformation(
                "AI inference available through {ProviderCount} provider(s), in order: {Providers}",
                _providers.Count,
                string.Join(" then ", _providers.Select(provider => $"{provider.Name} ({provider.Model})")));
        }
    }

    /// <inheritdoc />
    public bool IsLiveInferenceAvailable => _providers.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<ProvisionedAgent> BuildAgentChain(
        string instructions,
        string agentName,
        IReadOnlyList<AITool>? tools = null)
    {
        // Constructed directly rather than through an extension method. The agent-creation
        // extensions on IChatClient have been renamed across framework versions; this
        // constructor has not moved.
        return
        [
            .. _providers.Select(provider => new ProvisionedAgent(
                provider.Name,
                new ChatClientAgent(
                    provider.ChatClient,
                    instructions,
                    agentName,
                    null,
                    tools is { Count: > 0 } ? [.. tools] : null,
                    _loggerFactory)))
        ];
    }

    private static List<ConfiguredProvider> BuildConfiguredProviders(
        AgentConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var configured = new List<ConfiguredProvider>();

        foreach (var options in configuration.Providers)
        {
            var apiKey = options.ReadApiKey();

            if (string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(options.Endpoint) ||
                string.IsNullOrWhiteSpace(options.Model))
            {
                continue;
            }

            var openAiClient = new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) });

            // Function invocation lets the agent call the delivery-data tools itself rather
            // than being handed a fixed context block. Logging wraps it so every call the
            // model makes is visible when an answer looks wrong.
            var chatClient = openAiClient
                .GetChatClient(options.Model)
                .AsIChatClient()
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory)
                .UseLogging(loggerFactory)
                .Build();

            configured.Add(new ConfiguredProvider(
                options.Name,
                options.Model,
                configuration.Temperature,
                configuration.MaxResponseTokens,
                chatClient));
        }

        return configured;
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.ChatClient.Dispose();
        }
    }

    private sealed record ConfiguredProvider(
        string Name,
        string Model,
        float Temperature,
        int MaxResponseTokens,
        IChatClient ChatClient);
}
