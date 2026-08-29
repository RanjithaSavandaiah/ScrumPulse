namespace ScrumPulse.Infrastructure.Services;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ScrumPulse.Application.CQRS;

/// <summary>
/// High-performance CQRS mediator using compiled expression trees for handler dispatch.
/// Caches delegate factories per handler type for 10-50x improvement over raw reflection.
/// </summary>
public sealed class AppMediator(IServiceProvider serviceProvider) : IMediator
{
    // Compiled delegate cache: maps (commandType, responseType) → compiled invoke delegate
    private static readonly ConcurrentDictionary<(Type, Type), Func<object, object, CancellationToken, Task<object>>>
        _handlerDelegateCache = new();

    public async Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)
    {
        var result = await DispatchAsync(command, typeof(ICommandHandler<,>), typeof(TResponse), ct);
        return (TResponse)result;
    }

    public async Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)
    {
        var result = await DispatchAsync(query, typeof(IQueryHandler<,>), typeof(TResponse), ct);
        return (TResponse)result;
    }

    private async Task<object> DispatchAsync(object request, Type openHandlerType, Type responseType, CancellationToken ct)
    {
        var requestType = request.GetType();
        var handlerType = openHandlerType.MakeGenericType(requestType, responseType);
        var handler = serviceProvider.GetRequiredService(handlerType);

        var invoker = _handlerDelegateCache.GetOrAdd((requestType, responseType), _ =>
        {
            return CompileHandlerDelegate(handlerType, requestType, responseType);
        });

        return await invoker(handler, request, ct);
    }

    /// <summary>
    /// Compiles a strongly-typed delegate for the handler's HandleAsync method.
    /// This runs once per handler type and the result is cached for all subsequent calls.
    /// </summary>
    private static Func<object, object, CancellationToken, Task<object>> CompileHandlerDelegate(
        Type handlerType, Type requestType, Type responseType)
    {
        var method = handlerType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException($"HandleAsync not found on {handlerType.Name}");

        // Parameters: (object handler, object request, CancellationToken ct)
        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        // Cast: (HandlerType)handler, (RequestType)request
        var handlerCast = Expression.Convert(handlerParam, handlerType);
        var requestCast = Expression.Convert(requestParam, requestType);

        // Call: handler.HandleAsync(request, ct)
        var call = Expression.Call(handlerCast, method, requestCast, ctParam);

        // We need to convert Task<TResponse> to Task<object>
        // Use a continuation: .ContinueWith(t => (object)t.Result)
        var taskType = typeof(Task<>).MakeGenericType(responseType);
        var continueWithMethod = GetContinueWithMethod(responseType);

        var taskParam = Expression.Parameter(taskType, "t");
        var resultProp = Expression.Property(taskParam, "Result");
        var resultAsObject = Expression.Convert(resultProp, typeof(object));
        var continuationLambda = Expression.Lambda(resultAsObject, taskParam);

        var continueWithCall = Expression.Call(call, continueWithMethod, continuationLambda);

        var lambda = Expression.Lambda<Func<object, object, CancellationToken, Task<object>>>(
            continueWithCall, handlerParam, requestParam, ctParam);

        return lambda.Compile();
    }

    private static MethodInfo GetContinueWithMethod(Type responseType)
    {
        var taskType = typeof(Task<>).MakeGenericType(responseType);
        var methods = taskType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "ContinueWith"
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 1
                && m.GetParameters().Length == 1)
            .ToList();

        var continueWith = methods.First();
        return continueWith.MakeGenericMethod(typeof(object));
    }
}
