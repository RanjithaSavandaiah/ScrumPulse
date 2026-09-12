namespace ScrumPulse.Tests.Architecture;

using Microsoft.Extensions.DependencyInjection;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.WorkItems;
using ScrumPulse.Application.CQRS.Blockers;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Infrastructure.Registration;
using Xunit;

public class CqrsHandlerScannerTests
{
    [Fact]
    public void AddCqrsHandlersFromAssembly_RegistersWorkItemHandlers()
    {
        var services = new ServiceCollection();

        services.AddCqrsHandlersFromAssembly(typeof(CreateWorkItemCommand).Assembly);

        // Verify work item handlers are registered
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<CreateWorkItemCommand, WorkItemDto>));
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<AdvanceWorkItemStageCommand, WorkItemDto>));
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetWorkItemsQuery, IEnumerable<WorkItemDto>>));
    }

    [Fact]
    public void AddCqrsHandlersFromAssembly_RegistersBlockerHandlers()
    {
        var services = new ServiceCollection();

        services.AddCqrsHandlersFromAssembly(typeof(CreateBlockerCommand).Assembly);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<CreateBlockerCommand, BlockerDto>));
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetBlockersQuery, IEnumerable<BlockerDto>>));
    }

    [Fact]
    public void AddCqrsHandlersFromAssembly_AllRegistrationsAreScoped()
    {
        var services = new ServiceCollection();

        services.AddCqrsHandlersFromAssembly(typeof(CreateWorkItemCommand).Assembly);

        foreach (var descriptor in services)
        {
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }

    [Fact]
    public void AddCqrsHandlersFromAssembly_DoesNotRegisterInterfaces()
    {
        var services = new ServiceCollection();

        services.AddCqrsHandlersFromAssembly(typeof(CreateWorkItemCommand).Assembly);

        // Verify no abstract classes or interfaces are registered as implementations
        foreach (var descriptor in services)
        {
            Assert.False(descriptor.ImplementationType?.IsAbstract ?? false);
            Assert.False(descriptor.ImplementationType?.IsInterface ?? false);
        }
    }
}
