namespace ScrumPulse.Application.CQRS;

public interface ICommand<TResponse> { }
public interface IQuery<TResponse> { }

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken ct = default);
}

public interface IMediator
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct = default);
    Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken ct = default);
}
