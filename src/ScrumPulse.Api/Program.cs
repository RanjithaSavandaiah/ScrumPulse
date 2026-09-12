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

// Enable legacy timestamp behavior in Npgsql to allow flexible DateTime mappings across PostgreSQL versions
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Suppress Server header at the Kestrel transport level
builder.WebHost.ConfigureKestrel(serverOptions => serverOptions.AddServerHeader = false);

// Ensure WebRoot directory exists and is discovered across Docker and dotnet run contexts
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (!File.Exists(Path.Combine(wwwrootPath, "index.html")))
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
        Path.Combine(Directory.GetCurrentDirectory(), "src", "ScrumPulse.Api", "wwwroot"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "ScrumPulse.Api", "wwwroot"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot")
    };
    foreach (var candidate in candidates)
    {
        var fullPath = Path.GetFullPath(candidate);
        if (File.Exists(Path.Combine(fullPath, "index.html")))
        {
            wwwrootPath = fullPath;
            builder.Environment.WebRootPath = fullPath;
            break;
        }
    }
}

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

    // Global sliding window: partitioned per IP with generous headroom in Dev/CI
    rateLimiterOptions.AddPolicy("global", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(clientIp, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = builder.Environment.IsDevelopment() ? 2000 : 120,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });
    });

    // Strict limiter for auth endpoints: partitioned per IP, testable with X-Test-Rate-Limit header
    rateLimiterOptions.AddPolicy("auth", context =>
    {
        var isTestTrigger = context.Request.Headers.ContainsKey("X-Test-Rate-Limit");
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = isTestTrigger ? "test-rl-partition" : clientIp;

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = isTestTrigger ? 3 : (builder.Environment.IsDevelopment() ? 500 : 5),
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // AI endpoints: partitioned per IP
    rateLimiterOptions.AddPolicy("ai", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetTokenBucketLimiter(clientIp, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = builder.Environment.IsDevelopment() ? 100 : 20,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5
        });
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

// ── Output Caching (Sub-second response for read-heavy executive & metrics endpoints) ──
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(b => b.Expire(TimeSpan.FromSeconds(15)).SetVaryByHeader("X-Team-Id"));
    options.AddPolicy("ExecutiveReports", b => b.Expire(TimeSpan.FromSeconds(30)).SetVaryByHeader("X-Team-Id"));
    options.AddPolicy("ShortLived", b => b.Expire(TimeSpan.FromSeconds(10)).SetVaryByHeader("X-Team-Id"));
});

var app = builder.Build();

// ── Middleware Pipeline (order matters!) ──────────────────────────────────

// 1. Security headers (runs on every response)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. Request logging with correlation IDs
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Multi-team Tenant Resolution
app.UseMiddleware<TenantMiddleware>();

// 4. Global exception handler (ProblemDetails RFC 7807)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// 5. Response compression
app.UseResponseCompression();

// 6. Response caching
app.UseOutputCache();

// -- Database Initialization --
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dbLogger = scope.ServiceProvider.GetService<ILogger<AppDbContext>>();
    await db.Database.EnsureCreatedAsync();
    var seedDemoData = app.Configuration.GetValue<bool>("SeedDemoData", false);
    await DbInitializer.SeedAsync(db, seedDemoData, dbLogger);
}

// -- Swagger --
app.UseSwagger();
app.UseSwaggerUI(swaggerUiOptions =>
{
    swaggerUiOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "ScrumPulse API v1");
    swaggerUiOptions.RoutePrefix = "swagger";
});

// ── Static Files & Routing ───────────────────────────────────────────────
var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);

var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootPath);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

app.UseRouting();

// 7. Rate limiting (must follow UseRouting so endpoint-specific metadata [EnableRateLimiting] is matched)
app.UseRateLimiter();

app.UseAuthorization();

// -- Health Probes --
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/health");

// -- Map Controllers --
app.MapControllers();

// -- SPA Fallback --
var indexHtmlPath = Path.Combine(wwwrootPath, "index.html");
if (File.Exists(indexHtmlPath))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");
