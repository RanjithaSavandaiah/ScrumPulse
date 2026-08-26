namespace ScrumPulse.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;
using ScrumPulse.Application.CQRS;

public class AppMediator(IServiceProvider serviceProvider) : IMediator
{
    public async Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);

        var method = handlerType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException($"HandleAsync not found on {handlerType.Name}");

        var task = (Task<TResponse>)method.Invoke(handler, [command, ct])!;
        return await task;
    }

    public async Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)
    {
        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);

        var method = handlerType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException($"HandleAsync not found on {handlerType.Name}");

        var task = (Task<TResponse>)method.Invoke(handler, [query, ct])!;
        return await task;
    }
}
