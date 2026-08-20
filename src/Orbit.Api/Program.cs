using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Orbit.Api.Endpoints;
using Orbit.Api.Errors;
using Orbit.Api.Tenancy;
using Orbit.Application;
using Orbit.Application.Abstractions;
using Orbit.Infrastructure;
using Orbit.Infrastructure.Identity;
using Orbit.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
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
    options.AddFixedWindowLimiter("bootstrap", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    // PERF-03: Fall back to a shared "unknown" partition only when no remote IP
    // is resolvable (should be rare with the ForwardedHeaders fix above).
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));
    // Slack webhook posts are external-service side effects (message sends); throttle
    // per authenticated user to prevent channel spam from a compromised/misused token.
    options.AddPolicy("slack-share", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }));

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
app.UseRateLimiter();
app.UseAuthentication();
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
if (app.Environment.IsProduction() || !app.Configuration.GetValue<bool>("Tenancy:AllowHeaderTenant"))
{
    api.RequireAuthorization();
}
api.MapChoiceEndpoints();
api.MapWorkItemTypeEndpoints();
api.MapCustomFieldEndpoints();
api.MapIdentityEndpoints();
api.MapWorkspaceEndpoints();
api.MapSettingsEndpoints();
api.MapAccessEndpoints();
api.MapInvitationAdminEndpoints();
api.MapTeamEndpoints();
api.MapGroupEndpoints();
api.MapProjectEndpoints();
api.MapWorkItemEndpoints();
api.MapSlackEndpoints();
api.MapBoardEndpoints();
api.MapSprintEndpoints();

app.Run();

public partial class Program;
