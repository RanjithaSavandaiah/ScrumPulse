namespace ScrumPulse.Tests.Middleware;

using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScrumPulse.Api.Middleware;
using Xunit;

public class TestHostEnvironment : IHostEnvironment
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
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var middleware = new GlobalExceptionHandlerMiddleware(next, logger, env);

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.True(nextExecuted);
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionOccursInDevelopment_ReturnsProblemDetailsWithStackTrace()
    {
        RequestDelegate next = (HttpContext context) => throw new InvalidOperationException("Test dev failure");

        var logger = LoggerFactory.Create(builder => {}).CreateLogger<GlobalExceptionHandlerMiddleware>();
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
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
        RequestDelegate next = (HttpContext context) => throw new InvalidOperationException("Sensitive DB Connection String");

        var logger = LoggerFactory.Create(builder => {}).CreateLogger<GlobalExceptionHandlerMiddleware>();
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
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
}
