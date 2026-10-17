namespace ScrumPulse.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.WorkItems;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Sagas.WorkItemCompletion;
using ScrumPulse.Application.Services;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Registration;
using ScrumPulse.Infrastructure.Repositories;
using ScrumPulse.Infrastructure.Security;
using ScrumPulse.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Database Provider Configuration ----
        var databaseProvider = configuration["DatabaseProvider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ScrumPulse.db";

        if (databaseProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedConnStr = NormalizePostgresConnectionString(connectionString);
            services.AddDbContext<AppDbContext>(dbContextOptions =>
                dbContextOptions.UseNpgsql(normalizedConnStr, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null
                    );
                }));
        }
        else
        {
            services.AddDbContext<AppDbContext>(dbContextOptions =>
                dbContextOptions.UseSqlite(connectionString, sqliteOptions =>
                    sqliteOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        }

        services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());

        // -- Core Infrastructure Services ---
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IMetricsCalculatorService, MetricsCalculatorService>();
        services.AddScoped<ITeamPerformanceService, TeamPerformanceService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped(typeof(IAsyncRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // --- Domain Events ----
        // Scoped: dispatcher uses IServiceProvider to resolve handlers per request
        services.AddScoped<DomainEventDispatcher>();

        // -- Idempotency Store + Background Cleanup --
        services.AddSingleton<MemoryIdempotencyStore>();
        services.AddSingleton<IIdempotencyStore>(sp => sp.GetRequiredService<MemoryIdempotencyStore>());
        services.AddHostedService<IdempotencyCleanupService>();

        // -- CQRS Mediator & Auto-Discovered Handlers (Open-Closed Principle) --
        services.AddScoped<IMediator, AppMediator>();
        services.AddCqrsHandlersFromAssembly(typeof(CreateWorkItemCommand).Assembly);

        // -- Saga Orchestration --
        services.AddScoped<ValidateQualityGatesStep>();
        services.AddScoped<TransitionWorkItemStatusStep>();
        services.AddScoped<RecalculateSprintVelocityStep>();
        services.AddScoped<TriggerMicrosoftAgentAiCoachingStep>();
        services.AddScoped(sp => new WorkItemCompletionSaga(
            sp.GetRequiredService<ValidateQualityGatesStep>(),
            sp.GetRequiredService<TransitionWorkItemStatusStep>(),
            sp.GetRequiredService<RecalculateSprintVelocityStep>(),
            sp.GetRequiredService<TriggerMicrosoftAgentAiCoachingStep>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetService<ILogger<WorkItemCompletionSaga>>()
        ));

        return services;
    }

    /// <summary>
    /// Registers the signing key and token issuer backing team sessions.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration consulted for the signing key.</param>
    /// <param name="isDevelopment">
    /// When true, an ephemeral signing key is allowed so the app runs locally with no setup.
    /// Anywhere else a missing key stops startup rather than silently weakening every session.
    /// </param>
    /// <returns>The resolved signing key, so the host can configure token validation against it.</returns>
    public static SessionSigningKey AddSessionSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var signingKey = SessionSigningKey.Resolve(configuration, isDevelopment);

        services.AddSingleton(signingKey);
        services.AddSingleton<ISessionTokenIssuer, JwtSessionTokenIssuer>();

        return signingKey;
    }

    private static string NormalizePostgresConnectionString(string connStr)
    {
        if (string.IsNullOrWhiteSpace(connStr)) return connStr;

        string normalized;

        if (connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(connStr);
                var userInfo = uri.UserInfo.Split(':');
                var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
                var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.AbsolutePath.TrimStart('/');

                normalized = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScrumPulse] Warning: Failed to parse PostgreSQL connection URI: {ex.Message}. Falling back to raw connection string.");
                normalized = connStr;
            }
        }
        else
        {
            normalized = connStr;
        }

        if (!normalized.Contains("GSS Encryption Mode", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.TrimEnd(';') + ";GSS Encryption Mode=Disable;";
        }

        return normalized;
    }
}
