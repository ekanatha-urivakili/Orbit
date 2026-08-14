using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Orbit.Application.Identity;

namespace Orbit.IntegrationTests;

/// <summary>
/// Exercises the service-account credential lifecycle against real Postgres/RLS: creation under an
/// authenticated (dev-bypass) tenant admin, then the pre-auth <c>/auth/service-token</c> exchange.
/// Confirms the minted JWT carries exactly the claims <c>TenantTransactionMiddleware.ResolveIdentity</c>
/// requires for a service-account principal (client_id/principal_type/tenant_id) rather than
/// re-driving a full second HTTP request through the bearer-auth pipeline, which would need a real
/// bootstrapped user/login flow this suite doesn't otherwise set up.
/// </summary>
public sealed class ServiceAccountTests : IClassFixture<ServiceAccountApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client;

    public ServiceAccountTests(ServiceAccountApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateThenIssueToken_ProducesATokenWithServiceAccountClaims()
    {
        var tenantId = Guid.NewGuid();
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/service-accounts")
        {
            Content = JsonContent.Create(new { role = "Member" }),
        };
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceAccountCredentialDto>(JsonOptions);

        var tokenResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/service-token",
            new { clientId = created!.ClientId, clientSecret = created.ClientSecret });
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<AccessTokenDto>(JsonOptions);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token!.AccessToken);
        Assert.Equal(created.ClientId, jwt.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal("service_account", jwt.Claims.Single(claim => claim.Type == "principal_type").Value);
        Assert.Equal(tenantId.ToString(), jwt.Claims.Single(claim => claim.Type == "tenant_id").Value);
    }

    [Fact]
    public async Task IssueToken_RejectsWrongSecret()
    {
        var tenantId = Guid.NewGuid();
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/service-accounts")
        {
            Content = JsonContent.Create(new { role = "Member" }),
        };
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceAccountCredentialDto>(JsonOptions);

        var tokenResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/service-token",
            new { clientId = created!.ClientId, clientSecret = "wrong-secret" });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, tokenResponse.StatusCode);
    }

    [Fact]
    public async Task Rotate_InvalidatesThePreviousSecret()
    {
        var tenantId = Guid.NewGuid();
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/service-accounts")
        {
            Content = JsonContent.Create(new { role = "Member" }),
        };
        createRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceAccountCredentialDto>(JsonOptions);

        using var rotateRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/service-accounts/{created!.MembershipId}/rotate");
        rotateRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var rotateResponse = await _client.SendAsync(rotateRequest);
        rotateResponse.EnsureSuccessStatusCode();
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<ServiceAccountCredentialDto>(JsonOptions);

        Assert.Equal(created.ClientId, rotated!.ClientId);
        Assert.NotEqual(created.ClientSecret, rotated.ClientSecret);

        var oldTokenResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/service-token",
            new { clientId = created.ClientId, clientSecret = created.ClientSecret });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, oldTokenResponse.StatusCode);

        var newTokenResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/service-token",
            new { clientId = rotated.ClientId, clientSecret = rotated.ClientSecret });
        newTokenResponse.EnsureSuccessStatusCode();
    }
}

public sealed class ServiceAccountApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "orbit_local";
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:Postgres",
            $"Host=localhost;Database=orbit_test;Username=orbit;Password={password}");
        builder.UseSetting("Tenancy:AllowHeaderTenant", "true");
        builder.UseSetting("DatabaseSecurity:EnforceRuntimeRole", "false");
        builder.UseSetting("Authentication:Local:SigningKey", "28pi4E4/Nl5B9hSOGjbMRtG48Xd5ujuxtGN/wm+NEGI=");
    }
}
