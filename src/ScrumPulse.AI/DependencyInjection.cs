namespace ScrumPulse.AI;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Evaluation;
using ScrumPulse.AI.Prompt;
using ScrumPulse.AI.Services;
using ScrumPulse.Application.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Agent Harness Configuration
        var agentConfig = new AgentConfiguration();
        configuration.GetSection("AgentConfiguration").Bind(agentConfig);
        services.AddSingleton(agentConfig);

        // AI Service & Components
        services.AddScoped<IAiAgentService, MicrosoftAgentService>();
        services.AddSingleton<AiResponseEvaluator>();

        return services;
    }
}
