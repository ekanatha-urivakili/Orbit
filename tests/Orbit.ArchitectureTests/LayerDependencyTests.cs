using NetArchTest.Rules;
using Orbit.Domain.WorkItems;

namespace Orbit.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_DoesNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(typeof(WorkItem).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Orbit.Application", "Orbit.Infrastructure", "Orbit.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain must remain independent of outer layers.");
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(Orbit.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Orbit.Infrastructure", "Orbit.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application may depend only on Domain.");
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(typeof(Orbit.Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Orbit.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Infrastructure must not depend on the API composition root.");
    }
}
