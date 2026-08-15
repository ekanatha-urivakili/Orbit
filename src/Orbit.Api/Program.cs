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
    options.AddFixedWindowLimiter("bootstrap", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
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
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
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

app.MapGroup("/api/v1").MapBootstrapEndpoints();
app.MapGroup("/api/v1").MapAuthEndpoints();
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
api.MapBoardEndpoints();
api.MapSprintEndpoints();

app.Run();

public partial class Program;
