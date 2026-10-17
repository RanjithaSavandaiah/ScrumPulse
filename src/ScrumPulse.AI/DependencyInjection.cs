namespace ScrumPulse.AI;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Evaluation;
using ScrumPulse.AI.Providers;
using ScrumPulse.AI.Retrieval;
using ScrumPulse.AI.Services;
using ScrumPulse.AI.Strategies;
using ScrumPulse.Application.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        var agentConfiguration = new AgentConfiguration();
        configuration.GetSection("AgentConfiguration").Bind(agentConfiguration);
        services.AddSingleton(agentConfiguration);

        // Singleton: holds the pooled HTTP connections behind each provider's chat client.
        // Registering it per request would stand up a new connection pool on every call.
        services.AddSingleton<IAgentProvider, OpenAiCompatibleAgentProvider>();

        // Singleton so today's request count survives between requests. The budget belongs to
        // the deployment's quota, not to any one caller.
        services.AddSingleton(serviceProvider => new DailyRequestBudget(
            agentConfiguration.Providers.Sum(provider => provider.DailyRequestBudget),
            TimeProvider.System,
            serviceProvider.GetRequiredService<ILogger<DailyRequestBudget>>()));

        services.AddSingleton<AiResponseEvaluator>();

        // Scoped: reads through the request-scoped DbContext and so inherits that request's
        // tenant filter. This is what keeps the copilot's tools inside the caller's squad.
        services.AddScoped<IDeliveryMetricsRetriever, DeliveryMetricsRetriever>();

        // Rule-based fallbacks, served when inference is unavailable. Stateless, so one
        // instance of each is enough.
        services.AddSingleton<IInsightGenerator, IndividualInsightGenerator>();
        services.AddSingleton<IInsightGenerator, SprintInsightGenerator>();
        services.AddSingleton<IInsightGenerator, CompanyInsightGenerator>();

        services.AddScoped<IAiAgentService, MicrosoftAgentService>();
        services.AddScoped<IStandupTranscriptService, StandupTranscriptService>();

        // Typed client so the handler and its connection pool are managed by the factory
        // rather than a socket being opened per transcription. The timeout is generous
        // because Whisper is transcribing tens of seconds of audio, not answering a prompt.
        services
            .AddHttpClient<IAudioTranscriptionService, GroqWhisperTranscriptionService>(
                GroqWhisperTranscriptionService.HttpClientName,
                httpClient => httpClient.Timeout = TimeSpan.FromSeconds(90));

        return services;
    }
}
