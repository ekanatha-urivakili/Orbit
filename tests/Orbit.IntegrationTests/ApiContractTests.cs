using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Orbit.Api.Tenancy;
using Orbit.Application.Choices;
using Orbit.Domain.Access;

namespace Orbit.IntegrationTests;

public sealed class ApiContractTests : IClassFixture<OrbitApiFactory>
{
    private readonly HttpClient _client;

    public ApiContractTests(OrbitApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Choices_ReturnDefinedSystemValues()
    {
        var response = await _client.GetAsync("/api/v1/choices");
        var choices = await response.Content.ReadFromJsonAsync<SystemChoicesDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(choices!.WorkItemStatuses, choice => choice.Value == "InProgress");
        Assert.Contains(choices.Priorities, choice => choice.Value == "Highest");
        Assert.Equal(
            ["Initiative", "Epic", "Task", "Story", "Bug", "Spike", "Test", "Feature", "Request"],
            choices.WorkItemTypes.Where(choice => choice.Enabled).Select(choice => choice.Value));
    }

    [Fact]
    public async Task TenantScopedEndpoint_RejectsMissingTenantHeader()
    {
        var response = await _client.GetAsync("/api/v1/projects");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void DevelopmentTenant_ResolvesAnExplicitPrincipalContext()
    {
        var tenantId = Guid.NewGuid();
        var principal = new CurrentPrincipal();

        principal.SetDevelopmentPrincipal(tenantId);

        Assert.Equal(tenantId, principal.MembershipId);
        Assert.Equal(PrincipalType.User, principal.PrincipalType);
        Assert.Equal(TenantRole.Owner, principal.TenantRole);
        Assert.True(principal.IsDevelopmentBypass);
    }
}

public sealed class OrbitApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:Postgres",
            "Host=localhost;Database=orbit_test;Username=orbit;Password=unused");
        builder.UseSetting("Tenancy:AllowHeaderTenant", "true");
        builder.UseSetting("DatabaseSecurity:EnforceRuntimeRole", "false");
    }
}
