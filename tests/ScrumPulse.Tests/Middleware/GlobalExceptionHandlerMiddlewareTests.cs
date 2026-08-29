namespace ScrumPulse.Tests.Middleware;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScrumPulse.Api.Middleware;
using Xunit;

public class MiddlewareTestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "ScrumPulse.Api";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class GlobalExceptionHandlerMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNoException_PassesThroughToNext()
    {
        var nextExecuted = false;
        RequestDelegate next = (HttpContext context) =>
        {
            nextExecuted = true;
            return Task.CompletedTask;
        };

        var logger = LoggerFactory.Create(builder => {}).CreateLogger<GlobalExceptionHandlerMiddleware>();
        var env = new MiddlewareTestHostEnvironment { EnvironmentName = Environments.Production };
        var middleware = new GlobalExceptionHandlerMiddleware(next, logger, env);

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.True(nextExecuted);
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionOccursInDevelopment_ReturnsProblemDetailsWithStackTrace()
    {
        // Use a generic Exception which maps to 500 (InternalServerError)
        RequestDelegate next = (HttpContext context) => throw new Exception("Test dev failure");

        var logger = LoggerFactory.Create(builder => {}).CreateLogger<GlobalExceptionHandlerMiddleware>();
        var env = new MiddlewareTestHostEnvironment { EnvironmentName = Environments.Development };
        var middleware = new GlobalExceptionHandlerMiddleware(next, logger, env);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(problemDetails);
        Assert.Equal(500, problemDetails.Status);
        Assert.Equal("Test dev failure", problemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionOccursInProduction_HidesStackTrace()
    {
        // Use a generic Exception which maps to 500
        RequestDelegate next = (HttpContext context) => throw new Exception("Sensitive DB Connection String");

        var logger = LoggerFactory.Create(builder => {}).CreateLogger<GlobalExceptionHandlerMiddleware>();
        var env = new MiddlewareTestHostEnvironment { EnvironmentName = Environments.Production };
        var middleware = new GlobalExceptionHandlerMiddleware(next, logger, env);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(problemDetails);
        Assert.Equal("An internal error occurred. Please contact system support.", problemDetails.Detail);
        Assert.DoesNotContain("Sensitive DB Connection String", responseBody);
    }

    [Fact]
    public async Task InvokeAsync_ClassifiesExceptionTypes_Correctly()
    {
        var logger = LoggerFactory.Create(builder => {}).CreateLogger<GlobalExceptionHandlerMiddleware>();
        var env = new MiddlewareTestHostEnvironment { EnvironmentName = Environments.Development };

        // ArgumentException → 400
        var argMiddleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new ArgumentException("Bad arg"), logger, env);
        var argCtx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await argMiddleware.InvokeAsync(argCtx);
        Assert.Equal(400, argCtx.Response.StatusCode);

        // KeyNotFoundException → 404
        var notFoundMiddleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new KeyNotFoundException("Not found"), logger, env);
        var notFoundCtx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await notFoundMiddleware.InvokeAsync(notFoundCtx);
        Assert.Equal(404, notFoundCtx.Response.StatusCode);

        // InvalidOperationException → 409
        var conflictMiddleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("Conflict"), logger, env);
        var conflictCtx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await conflictMiddleware.InvokeAsync(conflictCtx);
        Assert.Equal(409, conflictCtx.Response.StatusCode);

        // UnauthorizedAccessException → 403
        var forbidMiddleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new UnauthorizedAccessException("Forbidden"), logger, env);
        var forbidCtx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await forbidMiddleware.InvokeAsync(forbidCtx);
        Assert.Equal(403, forbidCtx.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_IncludesCorrelationId_InResponse()
    {
        RequestDelegate next = _ => throw new Exception("corr test");
        var logger = LoggerFactory.Create(builder => {}).CreateLogger<GlobalExceptionHandlerMiddleware>();
        var env = new MiddlewareTestHostEnvironment { EnvironmentName = Environments.Development };
        var middleware = new GlobalExceptionHandlerMiddleware(next, logger, env);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items["CorrelationId"] = "test-corr-123";

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("test-corr-123", body);
    }
}
