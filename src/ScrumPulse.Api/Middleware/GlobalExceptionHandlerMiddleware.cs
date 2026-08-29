namespace ScrumPulse.Api.Middleware;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Global exception handler producing RFC 7807 ProblemDetails responses.
/// Classifies exception types to appropriate HTTP status codes and
/// propagates correlation IDs for request tracing.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false // compact for production
    };

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception unhandledException)
        {
            var correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

            _logger.LogError(unhandledException,
                "Unhandled {ExceptionType} during {Method} {Path} [CID: {CorrelationId}]",
                unhandledException.GetType().Name, httpContext.Request.Method,
                httpContext.Request.Path, correlationId);

            await HandleExceptionAsync(httpContext, unhandledException, correlationId);
        }
    }

    private Task HandleExceptionAsync(HttpContext httpContext, Exception exception, string correlationId)
    {
        var (statusCode, title) = ClassifyException(exception);

        httpContext.Response.ContentType = "application/problem+json";
        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = title,
            Detail = _environment.IsDevelopment() ? exception.Message : "An internal error occurred. Please contact system support.",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow.ToString("O");

        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
        }

        var jsonResponse = JsonSerializer.Serialize(problemDetails, JsonOptions);
        return httpContext.Response.WriteAsync(jsonResponse);
    }

    /// <summary>
    /// Maps exception types to appropriate HTTP status codes.
    /// </summary>
    private static (int StatusCode, string Title) ClassifyException(Exception exception) => exception switch
    {
        ArgumentException or ArgumentNullException =>
            ((int)HttpStatusCode.BadRequest, "Invalid request parameters."),

        KeyNotFoundException =>
            ((int)HttpStatusCode.NotFound, "The requested resource was not found."),

        InvalidOperationException =>
            ((int)HttpStatusCode.Conflict, "The operation could not be completed due to a conflict."),

        UnauthorizedAccessException =>
            ((int)HttpStatusCode.Forbidden, "Access to this resource is denied."),

        OperationCanceledException =>
            (499, "The request was cancelled by the client."), // nginx-style

        TimeoutException =>
            ((int)HttpStatusCode.GatewayTimeout, "The operation timed out."),

        _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred while processing your request.")
    };
}
