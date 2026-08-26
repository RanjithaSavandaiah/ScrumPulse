namespace ScrumPulse.Application.Sagas;

public interface ISagaStep<TContext>
{
    string StepName { get; }
    Task<bool> ExecuteAsync(TContext context, CancellationToken ct = default);
    Task CompensateAsync(TContext context, CancellationToken ct = default);
}

public interface ISagaOrchestrator<TContext, TResult>
{
    Task<SagaExecutionResult<TResult>> ExecuteAsync(TContext context, CancellationToken ct = default);
}

public record SagaExecutionResult<TResult>(
    bool IsSuccessful,
    TResult? Result,
    string? ErrorMessage,
    List<string> ExecutedSteps,
    List<string> CompensatedSteps
);
