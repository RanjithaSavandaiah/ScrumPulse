namespace ScrumPulse.Api.Middleware;

using System.Diagnostics;

/// <summary>
/// Structured request/response logging middleware with correlation ID propagation,
/// request timing, and response status tracking.
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext httpContext)
    {
        // Generate or propagate correlation ID
        var correlationId = httpContext.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N")[..12];

        httpContext.Items["CorrelationId"] = correlationId;
        httpContext.Response.Headers[CorrelationIdHeader] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        var path = httpContext.Request.Path;
        var method = httpContext.Request.Method;

        try
        {
            await next(httpContext);
            stopwatch.Stop();

            var statusCode = httpContext.Response.StatusCode;
            var level = statusCode >= 500 ? LogLevel.Error : (statusCode >= 400 ? LogLevel.Warning : LogLevel.Information);

            logger.Log(level,
                "{Method} {Path} → {StatusCode} in {ElapsedMs}ms [CID: {CorrelationId}]",
                method, path, statusCode, stopwatch.ElapsedMilliseconds, correlationId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex,
                "{Method} {Path} → UNHANDLED in {ElapsedMs}ms [CID: {CorrelationId}]",
                method, path, stopwatch.ElapsedMilliseconds, correlationId);
            throw;
        }
    }
}
