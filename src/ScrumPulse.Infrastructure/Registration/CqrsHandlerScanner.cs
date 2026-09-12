namespace ScrumPulse.Infrastructure.Registration;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ScrumPulse.Application.CQRS;

/// <summary>
/// Assembly-scanning CQRS handler registration following the Open-Closed Principle.
/// New command/query handlers are automatically discovered and registered without
/// modifying the DI configuration (no manual <c>services.AddScoped&lt;ICommandHandler&lt;...&gt;&gt;</c> needed).
/// </summary>
public static class CqrsHandlerScanner
{
    /// <summary>
    /// Scans the specified assembly for all implementations of
    /// <see cref="ICommandHandler{TCommand, TResponse}"/> and <see cref="IQueryHandler{TQuery, TResponse}"/>
    /// and registers them as scoped services.
    /// </summary>
    public static IServiceCollection AddCqrsHandlersFromAssembly(
        this IServiceCollection services, Assembly assembly)
    {
        var handlerInterfaceTypes = new[]
        {
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>)
        };

        var concreteHandlers = assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(iface => iface.IsGenericType &&
                                handlerInterfaceTypes.Contains(iface.GetGenericTypeDefinition()))
                .Select(iface => new { Interface = iface, Implementation = type }));

        foreach (var handler in concreteHandlers)
        {
            services.AddScoped(handler.Interface, handler.Implementation);
        }

        return services;
    }
}
