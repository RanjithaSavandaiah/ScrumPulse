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
        var databaseProvider = configuration["DatabaseProvider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ScrumPulse.db";

        if (databaseProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<AppDbContext>(dbContextOptions =>
                dbContextOptions.UseNpgsql(connectionString, npgsqlOptions =>
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
                dbContextOptions.UseSqlite(connectionString, sqliteOptions => sqliteOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        }

        services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IMetricsCalculatorService, MetricsCalculatorService>();
        
        // Repositories & Unit of Work
        services.AddScoped(typeof(IAsyncRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Domain Events & Idempotency Store
        services.AddSingleton<DomainEventDispatcher>();
        services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();

        // CQRS Mediator & Handlers
        services.AddScoped<IMediator, AppMediator>();
        services.AddScoped<IQueryHandler<GetWorkItemsQuery, IEnumerable<WorkItemDto>>, GetWorkItemsQueryHandler>();
        services.AddScoped<ICommandHandler<CreateWorkItemCommand, WorkItemDto>, CreateWorkItemCommandHandler>();
        services.AddScoped<ICommandHandler<AdvanceWorkItemStageCommand, WorkItemDto>, AdvanceWorkItemStageCommandHandler>();
        
        services.AddScoped<IQueryHandler<GetBlockersQuery, IEnumerable<BlockerDto>>, GetBlockersQueryHandler>();
        services.AddScoped<ICommandHandler<CreateBlockerCommand, BlockerDto>, CreateBlockerCommandHandler>();
        services.AddScoped<ICommandHandler<ResolveBlockerCommand, BlockerDto?>, ResolveBlockerCommandHandler>();

        // Sagas & Steps
        services.AddScoped<ValidateQualityGatesStep>();
        services.AddScoped<TransitionWorkItemStatusStep>();
        services.AddScoped<RecalculateSprintVelocityStep>();
        services.AddScoped<TriggerMicrosoftAgentAiCoachingStep>();
        services.AddScoped<WorkItemCompletionSaga>();

        return services;
    }
}
