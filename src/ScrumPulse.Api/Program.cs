using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using ScrumPulse.AI;
using ScrumPulse.Api.Middleware;
using ScrumPulse.Api.Security;
using ScrumPulse.Infrastructure;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Security;

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
        Path.Combine(Directory.GetCurrentDirectory(), "src", "ScrumPulse.UI", "dist", "scrum-pulse.ui", "browser"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "ScrumPulse.UI", "dist", "scrum-pulse.ui", "browser"),
        Path.Combine(builder.Environment.ContentRootPath, "..", "ScrumPulse.UI", "dist", "scrum-pulse.ui", "browser"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ScrumPulse.UI", "dist", "scrum-pulse.ui", "browser")
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
        Title = "ScrumPulse API",
        Version = "v1",
        Description = "Engineering velocity, lifecycle latencies, blocker SLAs, and AI delivery coaching."
    });
});

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddAiServices(builder.Configuration);

// Team and role come from a signed token this server issued, never from a request header.
// Outside development a missing SESSION_SIGNING_KEY fails startup rather than silently
// weakening every session that follows.
var sessionSigningKey = builder.Services.AddSessionSecurity(
    builder.Configuration,
    builder.Environment.IsDevelopment());

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwtOptions =>
    {
        jwtOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = JwtSessionTokenIssuer.Issuer,
            ValidateAudience = true,
            ValidAudience = JwtSessionTokenIssuer.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = sessionSigningKey.Key,
            ValidateLifetime = true,

            // The default five minutes of clock skew extends the life of every expired
            // token. Sessions are issued and validated by this same process, so no skew
            // allowance is warranted.
            ClockSkew = TimeSpan.Zero
        };

        jwtOptions.Events = new JwtBearerEvents
        {
            // The token travels in an HttpOnly cookie so that page script cannot read it.
            // JwtBearer only looks at the Authorization header by default, so the cookie is
            // lifted onto the request here.
            OnMessageReceived = messageContext =>
            {
                var cookieName = SessionCookie.ResolveName(messageContext.Request.IsHttps);

                if (messageContext.Request.Cookies.TryGetValue(cookieName, out var token) &&
                    !string.IsNullOrWhiteSpace(token))
                {
                    messageContext.Token = token;
                }

                return Task.CompletedTask;
            },

            // An expired or tampered token should look the same as no token at all: the
            // caller is anonymous and gets a 401 from the fallback policy. Returning the
            // validation detail would tell an attacker which part of the token to adjust.
            OnAuthenticationFailed = failureContext =>
            {
                failureContext.NoResult();
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    // Secure by default: every controller endpoint requires a session unless it opts out with
    // [AllowAnonymous]. Without this a newly added controller is reachable by anyone, which is
    // how fourteen mutating endpoints were left unprotected before.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

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

    // Strict limiter for the two endpoints that accept a secret: joining a squad and
    // elevating to Scrum Master. Five attempts a minute makes guessing a join code or a
    // four digit PIN impractical.
    //
    // This previously honoured an X-Test-Rate-Limit request header that tightened the limit
    // to three and moved the caller into a shared partition. Test scaffolding reachable from
    // production let any caller exhaust that shared partition for everyone else, so the
    // limits are now fixed and the E2E suite drives the real one.
    rateLimiterOptions.AddPolicy("auth", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (builder.Environment.IsDevelopment() && context.Request.Headers.ContainsKey("X-Test-Rate-Limit"))
        {
            return RateLimitPartition.GetFixedWindowLimiter("test-auth-partition", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        }

        var endpoint = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var isVerifyPin = endpoint.Contains("verify-pin");

        // In Development, joining squads needs generous headroom because dozens of E2E tests
        // authenticate sequentially from localhost. Verifying PIN uses a burst limiter (5 permits
        // per 2s window in dev) so repeated brute-force PIN attempts are rejected with 429 without
        // starving subsequent tests. In production, both endpoints strictly allow 5 attempts per minute.
        var permitLimit = builder.Environment.IsDevelopment() && !isVerifyPin ? 500 : 5;
        var window = builder.Environment.IsDevelopment() && isVerifyPin
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromMinutes(1);

        var partitionKey = $"{clientIp}:{endpoint}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // AI endpoints are partitioned per team rather than per IP. The upstream model quota is
    // a per-day allowance shared by the whole deployment, so one squad behind a corporate
    // NAT should not be able to spend it on behalf of everyone.
    rateLimiterOptions.AddPolicy("ai", context =>
    {
        var partitionKey = context.User.Identity?.IsAuthenticated == true
            ? $"team:{context.User.FindFirst(ScrumPulse.Application.Common.Security.ScrumPulseClaimTypes.TeamId)?.Value}"
            : $"ip:{context.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = builder.Environment.IsDevelopment() ? 100 : 20,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5
        });
    });
});

builder.Services.AddResponseCompression(compressionOptions =>
{
    // Compression stays off for HTTPS responses. Compressing a response that mixes a secret
    // with caller-controlled input is what makes BREACH possible, and the session token is
    // carried in a cookie on these same responses. Static assets are pre-compressed by the
    // Angular build and served by the CDN layer, so little is lost here.
    compressionOptions.EnableForHttps = false;
    compressionOptions.Providers.Add<BrotliCompressionProvider>();
    compressionOptions.Providers.Add<GzipCompressionProvider>();
    compressionOptions.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/json", "application/problem+json"]);
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = System.IO.Compression.CompressionLevel.Fastest);

builder.Services.AddCors(corsOptions =>
{
    corsOptions.AddPolicy("Production", corsPolicy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? ["https://scrumpulse.onrender.com", "http://localhost:4200"];

        corsPolicy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Required for the browser to send the session cookie on cross-origin calls
            // during local development, where the SPA runs on port 4200.
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });

    // Development-only permissive policy. AllowAnyOrigin cannot be combined with
    // AllowCredentials, so the dev origin is named explicitly to keep the cookie flowing.
    if (builder.Environment.IsDevelopment())
    {
        corsOptions.AddPolicy("Development", corsPolicy =>
            corsPolicy.WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    }
});

// Every policy keys on the requesting session's team. See TenantOutputCachePolicy for why
// varying on a request header would have leaked one squad's report to the next.
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policy => policy.AddPolicy<TenantOutputCachePolicy>(), excludeDefaultPolicy: true);
    options.AddPolicy("ExecutiveReports", new TenantOutputCachePolicy(TimeSpan.FromSeconds(30)));
    options.AddPolicy("ShortLived", new TenantOutputCachePolicy(TimeSpan.FromSeconds(10)));
});

var app = builder.Build();

if (sessionSigningKey.IsEphemeral)
{
    app.Logger.LogWarning(
        "SESSION_SIGNING_KEY is not set. A throwaway key was generated, so every session ends " +
        "when this process restarts. Set the variable before deploying.");
}

// Order matters. The exception handler runs first so that it also catches failures thrown by
// the middleware below it. It previously sat fourth, which left security header, logging and
// tenant resolution faults to surface as unhandled stack traces.

// 1. Global exception handler (ProblemDetails RFC 7807)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// 2. Security headers (runs on every response, including error responses)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 3. Request logging with correlation IDs
app.UseMiddleware<RequestLoggingMiddleware>();

// 4. Response compression
app.UseResponseCompression();

// -- Database Initialization --
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dbLogger = scope.ServiceProvider.GetService<ILogger<AppDbContext>>();

    // EnsureCreated builds the schema from the current model but cannot evolve an existing
    // database. The first schema change after go-live therefore needs a real migration
    // history; see the deployment section of the README.
    await db.Database.EnsureCreatedAsync();

    var seedDemoData = app.Configuration.GetValue<bool>("SeedDemoData", false);
    await DbInitializer.SeedAsync(db, seedDemoData, dbLogger);
}

// -- Swagger --
// Development only. In production the schema is a map of every endpoint, parameter and
// payload shape, which is free reconnaissance on a public deployment.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(swaggerUiOptions =>
    {
        swaggerUiOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "ScrumPulse API v1");
        swaggerUiOptions.RoutePrefix = "swagger";
    });
}

var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);

var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootPath);

// Serve prerendered pages before anything else claims the request.
//
// The Angular build emits one static HTML file per public route: /about/index.html,
// /blog/cycle-time-micro-stages/index.html and so on. A browser or crawler asks for "/about"
// with no trailing slash, which matches no file on disk, so without this rewrite the request
// would fall through to the SPA fallback below and receive the empty client-rendered shell.
// The prerendered article text would exist on disk and never be served, which defeats the
// point of generating it.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;

    if (HttpMethods.IsGet(context.Request.Method) &&
        !string.IsNullOrEmpty(path) &&
        !Path.HasExtension(path) &&
        !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
    {
        var prerenderedPage = $"{path.TrimEnd('/')}/index.html";

        if (fileProvider.GetFileInfo(prerenderedPage).Exists)
        {
            context.Request.Path = prerenderedPage;
        }
    }

    await next();
});

app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

app.UseRouting();

// 5. Rate limiting, after UseRouting so endpoint [EnableRateLimiting] metadata is matched
app.UseRateLimiter();

// 6. Authentication, then authorization. Both must precede the session context and output
//    cache, which read the resulting claims.
app.UseAuthentication();
app.UseAuthorization();

// 7. Project the validated session onto the tenant context used by the data layer
app.UseMiddleware<SessionContextMiddleware>();

// 8. Output caching, keyed per team from the claims resolved above
app.UseOutputCache();

// -- Health Probes --
app.MapHealthChecks("/healthz").AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

// -- Map Controllers --
app.MapControllers();

// -- SPA Fallback --
var indexHtmlPath = Path.Combine(wwwrootPath, "index.html");
if (File.Exists(indexHtmlPath))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider })
        .AllowAnonymous();
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");

/// <summary>
/// Exposed so the integration test host can reference the entry point assembly.
/// </summary>
public partial class Program;
