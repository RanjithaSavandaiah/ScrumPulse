using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.AI;
using ScrumPulse.Api.Middleware;
using ScrumPulse.Infrastructure;
using ScrumPulse.Infrastructure.Persistence;

// Prevent Linux container inotify limit exception on cloud hosting (Render/AWS/Kubernetes)
Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Ensure WebRoot directory exists so StaticFileMiddleware initializes with zero warnings
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}

// ── API Controllers & OpenAPI ────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(jsonOptions =>
    {
        jsonOptions.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swaggerGenOptions =>
{
    swaggerGenOptions.SwaggerDoc("v1", new()
    {
        Title = "ScrumPulse Enterprise API",
        Version = "v1",
        Description = "Engineering Velocity, Lifecycle Latencies, Blocker SLAs, and Microsoft AI Agent Framework Coach"
    });
});

// ── Clean Architecture Layer Registration ────────────────────────────────
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAiServices(builder.Configuration);

// ── Health Checks ────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// ── Rate Limiting (Critical for public site) ─────────────────────────────
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global sliding window: 60 requests per minute per IP
    rateLimiterOptions.AddSlidingWindowLimiter("global", slidingOptions =>
    {
        slidingOptions.PermitLimit = 60;
        slidingOptions.Window = TimeSpan.FromMinutes(1);
        slidingOptions.SegmentsPerWindow = 6;
        slidingOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        slidingOptions.QueueLimit = 5;
    });

    // Strict limiter for auth endpoints: 5 attempts per minute
    rateLimiterOptions.AddFixedWindowLimiter("auth", authOptions =>
    {
        authOptions.PermitLimit = 5;
        authOptions.Window = TimeSpan.FromMinutes(1);
        authOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        authOptions.QueueLimit = 0;
    });

    // AI endpoints: more generous but still bounded
    rateLimiterOptions.AddTokenBucketLimiter("ai", aiOptions =>
    {
        aiOptions.TokenLimit = 20;
        aiOptions.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        aiOptions.TokensPerPeriod = 10;
        aiOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        aiOptions.QueueLimit = 2;
    });
});

// ── Response Compression (Brotli + GZip) ─────────────────────────────────
builder.Services.AddResponseCompression(compressionOptions =>
{
    compressionOptions.EnableForHttps = true;
    compressionOptions.Providers.Add<BrotliCompressionProvider>();
    compressionOptions.Providers.Add<GzipCompressionProvider>();
    compressionOptions.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/json", "application/problem+json"]);
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = System.IO.Compression.CompressionLevel.Fastest);

// ── CORS (Hardened for public hosting) ───────────────────────────────────
builder.Services.AddCors(corsOptions =>
{
    corsOptions.AddPolicy("Production", corsPolicy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? ["https://scrumpulse.onrender.com", "http://localhost:4200"];

        corsPolicy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });

    // Development-only permissive policy
    if (builder.Environment.IsDevelopment())
    {
        corsOptions.AddPolicy("Development", corsPolicy =>
            corsPolicy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    }
});

var app = builder.Build();

// ── Middleware Pipeline (order matters!) ──────────────────────────────────

// 1. Security headers (runs on every response)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. Request logging with correlation IDs
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Global exception handler (ProblemDetails RFC 7807)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// 4. Response compression
app.UseResponseCompression();

// 5. Rate limiting
app.UseRateLimiter();

// ── Database Initialization ──────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var seedDemoData = app.Configuration.GetValue<bool>("SeedDemoData", false);
    await DbInitializer.SeedAsync(db, seedDemoData);
}

// ── Swagger ──────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(swaggerUiOptions =>
{
    swaggerUiOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "ScrumPulse API v1");
    swaggerUiOptions.RoutePrefix = "swagger";
});

// ── Static Files & Routing ───────────────────────────────────────────────
var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// ── Health Probes ────────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapHealthChecks("/healthz");

// ── Map Controllers with global rate limiting ────────────────────────────
app.MapControllers().RequireRateLimiting("global");

// ── SPA Fallback ─────────────────────────────────────────────────────────
var indexHtmlPath = Path.Combine(wwwrootPath, "index.html");
if (File.Exists(indexHtmlPath))
{
    app.MapFallbackToFile("index.html");
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");
