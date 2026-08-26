namespace ScrumPulse.AI;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScrumPulse.AI.Services;
using ScrumPulse.Application.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAiAgentService, MicrosoftAgentService>();
        return services;
    }
}
