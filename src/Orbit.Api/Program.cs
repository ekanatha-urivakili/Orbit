using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orbit.Api.Endpoints;
using Orbit.Api.Errors;
using Orbit.Api.Observability;
using Orbit.Api.Tenancy;
using Orbit.Application;
using Orbit.Application.Abstractions;
using Orbit.Application.Caching;
using Orbit.Infrastructure;
using Orbit.Infrastructure.Identity;
using Orbit.Infrastructure.Persistence;
using Orbit.Infrastructure.RateLimiting;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// §4.2: compact JSON to stdout outside Development so structured fields (CorrelationId, TraceId,
// TenantId - pushed via ILogger.BeginScope in CorrelationIdMiddleware/TenantTransactionMiddleware)
// are queryable rather than grepped from free text. Development keeps Serilog's readable console
// format. Mirrors the existing appsettings.json Logging:LogLevel defaults in code rather than
// adding a Serilog.Settings.Configuration dependency for a two-value override.
builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext();

    if (context.HostingEnvironment.IsDevelopment())
    {
        loggerConfiguration.WriteTo.Console();
    }
    else
    {
        loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
    }
});

const string bearerScheme = "OrbitBearer";
const string localBearerScheme = "OrbitLocalBearer";
const string externalBearerScheme = "OrbitExternalBearer";
var localIssuer = builder.Configuration[$"{LocalTokenOptions.SectionName}:Issuer"]
    ?? LocalTokenOptions.DefaultIssuer;
var externalAuthority = builder.Configuration["Authentication:Authority"];

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
builder.Services.AddScoped<CurrentPrincipal>();
builder.Services.AddScoped<ICurrentPrincipal>(provider => provider.GetRequiredService<CurrentPrincipal>());
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = bearerScheme;
        options.DefaultChallengeScheme = bearerScheme;
    })
    .AddPolicyScheme(bearerScheme, null, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var token = new JwtSecurityTokenHandler().ReadJwtToken(header["Bearer ".Length..].Trim());
                    return string.Equals(token.Issuer, localIssuer, StringComparison.Ordinal)
                        ? localBearerScheme
                        : externalBearerScheme;
                }
                catch (Exception)
                {
                }
            }

            return localBearerScheme;
        };
    })
    .AddJwtBearer(localBearerScheme, options =>
    {
        options.MapInboundClaims = false;
        var configuredKey = builder.Configuration[$"{LocalTokenOptions.SectionName}:SigningKey"];
        if (string.IsNullOrWhiteSpace(configuredKey) && builder.Environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Authentication:Local:SigningKey must be explicitly configured in production environments.");
        }

        var keyBytes = string.IsNullOrWhiteSpace(configuredKey)
            ? RandomNumberGenerator.GetBytes(32)
            : Convert.FromBase64String(configuredKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = localIssuer,
            ValidateAudience = true,
            ValidAudience = builder.Configuration[$"{LocalTokenOptions.SectionName}:Audience"]
                ?? LocalTokenOptions.DefaultAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    })
    .AddJwtBearer(externalBearerScheme, options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        if (!string.IsNullOrWhiteSpace(externalAuthority))
        {
            options.Authority = externalAuthority;
            options.Audience = builder.Configuration["Authentication:Audience"];
            options.TokenValidationParameters = new TokenValidationParameters { NameClaimType = "sub" };
            return;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "urn:orbit:external-authentication-disabled",
            ValidateAudience = true,
            ValidAudience = "orbit-external-authentication-disabled",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)),
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    // SEC-03: Emit 429 Too Many Requests with a Retry-After hint so clients
    // can implement proper back-off instead of hammering a 503.
    options.OnRejected = async (rejectionContext, cancellationToken) =>
    {
        rejectionContext.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        rejectionContext.HttpContext.Response.Headers.RetryAfter = "60";
        await rejectionContext.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.", cancellationToken);
    };
    // PERF-03: Fall back to a shared "unknown" partition only when no remote IP
    // is resolvable (should be rare with the ForwardedHeaders fix above).
    options.AddPolicy("bootstrap", context => CreateRateLimitPartition(
        context, "bootstrap", "global", permitLimit: 5, TimeSpan.FromMinutes(1)));
    options.AddPolicy("auth", context => CreateRateLimitPartition(
        context, "auth", context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        permitLimit: 20, TimeSpan.FromMinutes(1)));
    options.AddPolicy("api", context => CreateRateLimitPartition(
        context,
        "api",
        context.User.FindFirst("sub")?.Value is { } subject
            ? $"{subject}:{context.User.FindFirst("tenant_id")?.Value ?? "unknown"}"
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        permitLimit: 120,
        TimeSpan.FromMinutes(1)));
    // Slack webhook posts are external-service side effects (message sends); throttle
    // per authenticated user to prevent channel spam from a compromised/misused token.
    options.AddPolicy("slack-share", context => CreateRateLimitPartition(
        context, "slack-share",
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        permitLimit: 5, TimeSpan.FromMinutes(1)));
});

// §13.7.1 (ADR-022): use the Valkey-backed sliding-window limiter across every partition when
// RateLimiting:Distributed:Enabled=true and Redis is configured, so a caller round-robined across
// replicas is checked against one shared count instead of each replica's own in-memory window.
// Falls back to today's per-replica FixedWindowLimiter otherwise - zero behavior change for
// anyone who hasn't opted in.
static RateLimitPartition<string> CreateRateLimitPartition(
    HttpContext context, string policyName, string partitionKey, int permitLimit, TimeSpan window)
{
    var key = $"{policyName}:{partitionKey}";
    var distributedEnabled = context.RequestServices
        .GetRequiredService<IOptions<RateLimitingOptions>>().Value.Enabled;
    var connectionMultiplexer = context.RequestServices.GetService<IConnectionMultiplexer>();

    if (distributedEnabled && connectionMultiplexer is not null)
    {
        return RateLimitPartition.Get(
            key,
            _ => new RedisSlidingWindowRateLimiter(
                connectionMultiplexer, policyName, partitionKey, window, permitLimit));
    }

    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = window,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });
}
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Correlation-Id");
    }));

// §13.7.2 (ADR-023): exports to the orbit-otel Collector via OTLP (standard OTEL_EXPORTER_OTLP_*
// env vars, defaulting to http://localhost:4317). AddRedisInstrumentation()/AddNpgsql() resolve
// their connection from DI at build time and no-op if none is registered, so this is safe whether
// or not Redis is configured for this environment.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("orbit-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddRedisInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(RateLimitTelemetry.MeterName)
        .AddMeter(CacheTelemetry.MeterName)
        .AddMeter("Microsoft.Extensions.Caching.Hybrid")
        .AddOtlpExporter());

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // Only trust the immediately-upstream proxy (loopback + RFC-1918 ranges).
    // Leaving KnownIPNetworks unconstrained allows a spoofed X-Forwarded-For header
    // to bypass IP-keyed rate limiting (SEC-02). Uses System.Net.IPNetwork (.NET 10).
    KnownIPNetworks =
    {
        new System.Net.IPNetwork(System.Net.IPAddress.Parse("127.0.0.0"), 8),
        new System.Net.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8),
        new System.Net.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12),
        new System.Net.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16),
    },
});

if (app.Configuration.GetValue("DatabaseSecurity:EnforceRuntimeRole", app.Environment.IsProduction()))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<RuntimeDatabaseSecurityValidator>()
        .ValidateAsync(CancellationToken.None);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // API version policy (§13.5): every response reports the version it was served under, so
    // clients can detect a version change without a separate lookup. See VersionEndpoints for the
    // /api/version info endpoint and the deprecation/sunset header convention a future v2 will use.
    context.Response.Headers.Append("Api-Version", VersionEndpoints.CurrentVersion);
    // SEC-01: Content-Security-Policy. The API returns JSON; script execution and
    // framing should be completely denied. Adjust 'connect-src' when you add CDN
    // or WebSocket endpoints.
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'none'; frame-ancestors 'none'");
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<TenantTransactionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .ExcludeFromDescription();
app.MapGet("/health/ready", HealthEndpoints.ReadyAsync)
    .ExcludeFromDescription();
app.MapGet("/api/version", VersionEndpoints.GetVersion)
    .WithName("GetApiVersion")
    .WithTags("Version");

app.MapGroup("/api/v1").MapBootstrapEndpoints();
app.MapGroup("/api/v1").MapRegistrationEndpoints();
app.MapGroup("/api/v1").MapAuthEndpoints();
app.MapGroup("/api/v1").MapGoogleOAuthEndpoints();
app.MapGroup("/api/v1").MapInvitationAcceptanceEndpoints();

var api = app.MapGroup("/api/v1");
api.RequireRateLimiting("api");
if (app.Environment.IsProduction() || !app.Configuration.GetValue<bool>("Tenancy:AllowHeaderTenant"))
{
    api.RequireAuthorization();
}
api.MapChoiceEndpoints();
api.MapWorkItemTypeEndpoints();
api.MapWorkItemStatusEndpoints();
api.MapCustomFieldEndpoints();
api.MapIdentityEndpoints();
api.MapWorkspaceEndpoints();
api.MapSettingsEndpoints();
api.MapAccessEndpoints();
api.MapRoleEndpoints();
api.MapInvitationAdminEndpoints();
api.MapTeamEndpoints();
api.MapGroupEndpoints();
api.MapProjectEndpoints();
api.MapWorkItemEndpoints();
api.MapSlackEndpoints();
api.MapBoardEndpoints();
api.MapSprintEndpoints();
api.MapBoardViewPreferenceEndpoints();

app.Run();

public partial class Program;
