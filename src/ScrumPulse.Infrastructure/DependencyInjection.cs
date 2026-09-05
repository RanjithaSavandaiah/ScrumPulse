namespace ScrumPulse.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.Blockers;
using ScrumPulse.Application.CQRS.WorkItems;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Sagas.WorkItemCompletion;
using ScrumPulse.Application.Services;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Repositories;
using ScrumPulse.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Database Provider Configuration ──────────────────────────────
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

        // ── Core Infrastructure Services ─────────────────────────────────
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IMetricsCalculatorService, MetricsCalculatorService>();
        services.AddScoped<ITeamPerformanceService, TeamPerformanceService>();
        services.AddScoped(typeof(IAsyncRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // ── Domain Events ────────────────────────────────────────────────
        // Scoped: dispatcher uses IServiceProvider to resolve handlers per-request
        services.AddScoped<DomainEventDispatcher>();

        // ── Idempotency Store + Background Cleanup ──────────────────────
        services.AddSingleton<MemoryIdempotencyStore>();
        services.AddSingleton<IIdempotencyStore>(sp => sp.GetRequiredService<MemoryIdempotencyStore>());
        services.AddHostedService<IdempotencyCleanupService>();

        // ── CQRS Mediator ────────────────────────────────────────────────
        services.AddScoped<IMediator, AppMediator>();

        // ── WorkItem Handlers ────────────────────────────────────────────
        services.AddScoped<IQueryHandler<GetWorkItemsQuery, IEnumerable<WorkItemDto>>, GetWorkItemsQueryHandler>();
        services.AddScoped<ICommandHandler<CreateWorkItemCommand, WorkItemDto>, CreateWorkItemCommandHandler>();
        services.AddScoped<ICommandHandler<AdvanceWorkItemStageCommand, WorkItemDto>, AdvanceWorkItemStageCommandHandler>();

        // ── Blocker Handlers ─────────────────────────────────────────────
        services.AddScoped<IQueryHandler<GetBlockersQuery, IEnumerable<BlockerDto>>, GetBlockersQueryHandler>();
        services.AddScoped<ICommandHandler<CreateBlockerCommand, BlockerDto>, CreateBlockerCommandHandler>();
        services.AddScoped<ICommandHandler<ResolveBlockerCommand, BlockerDto?>, ResolveBlockerCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateBlockerCommand, BlockerDto?>, UpdateBlockerCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteBlockerCommand, bool>, DeleteBlockerCommandHandler>();

        // ── Saga Orchestration ───────────────────────────────────────────
        services.AddScoped<ValidateQualityGatesStep>();
        services.AddScoped<TransitionWorkItemStatusStep>();
        services.AddScoped<RecalculateSprintVelocityStep>();
        services.AddScoped<TriggerMicrosoftAgentAiCoachingStep>();
        services.AddScoped<WorkItemCompletionSaga>();

        return services;
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
            catch
            {
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
