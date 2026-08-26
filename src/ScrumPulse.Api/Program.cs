using Microsoft.EntityFrameworkCore;
using ScrumPulse.AI;
using ScrumPulse.Api.Middleware;
using ScrumPulse.Infrastructure;
using ScrumPulse.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Ensure WebRoot directory exists so StaticFileMiddleware initializes with zero warnings
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}

// Add API controllers with string enum serialization & OpenAPI documentation
builder.Services.AddControllers()
    .AddJsonOptions(jsonOptions =>
    {
        jsonOptions.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swaggerGenOptions =>
{
    swaggerGenOptions.SwaggerDoc("v1", new() { Title = "ScrumPulse Enterprise API", Version = "v1", Description = "Engineering Velocity, Lifecycle Latencies, Blocker SLAs, and Microsoft AI Agent Framework Coach" });
});

// Configure Clean Architecture Layers (Infrastructure & Dedicated AI Agent Framework)
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAiServices(builder.Configuration);

// Cloud Observability & Health Check Probes
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// CORS for public web hosting & Render deployment
builder.Services.AddCors(corsOptions =>
{
    corsOptions.AddPolicy("AllowAll", corsPolicy =>
    {
        corsPolicy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Global ProblemDetails (RFC 7807) exception handler middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Database migration & seed execution
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbInitializer.SeedAsync(db);
}

// Enable Swagger in all environments for API visibility
app.UseSwagger();
app.UseSwaggerUI(swaggerUiOptions =>
{
    swaggerUiOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "ScrumPulse API v1");
    swaggerUiOptions.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

// Serve Angular static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// Map Health Probes
app.MapHealthChecks("/health");
app.MapHealthChecks("/healthz");

app.MapControllers();

// SPA fallback for Angular client-side routes if index.html is present
var indexHtmlPath = Path.Combine(wwwrootPath, "index.html");
if (File.Exists(indexHtmlPath))
{
    app.MapFallbackToFile("index.html");
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");
