namespace ScrumPulse.Api.Middleware;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

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
            _logger.LogError(unhandledException, "An unhandled exception occurred during HTTP request execution: {Path}", httpContext.Request.Path);
            await HandleExceptionAsync(httpContext, unhandledException);
        }
    }

    private Task HandleExceptionAsync(HttpContext httpContext, Exception unhandledException)
    {
        httpContext.Response.ContentType = "application/problem+json";
        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Type = "https://datatracker.ietf.org/doc/html/rfc7807",
            Title = "An unexpected error occurred while processing your request.",
            Detail = _environment.IsDevelopment() ? unhandledException.Message : "An internal error occurred. Please contact system support.",
            Instance = httpContext.Request.Path
        };

        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = unhandledException.StackTrace;
        }

        var jsonResponse = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return httpContext.Response.WriteAsync(jsonResponse);
    }
}
