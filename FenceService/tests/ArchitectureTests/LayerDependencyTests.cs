using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace ArchitectureTests;

internal static class Layers
{
    internal static readonly Assembly Domain = typeof(Domain.DomainMarker).Assembly;

    internal const string ApplicationNamespace = "Application";
    internal static readonly Assembly Application = typeof(Application.ApplicationMarker).Assembly;

    internal const string InfrastructureNamespace = "Infrastructure";
    internal static readonly Assembly Infrastructure = typeof(Infrastructure.InfrastructureMarker).Assembly;

    internal const string ApiNamespace = "Api";
    internal static readonly Assembly Api = typeof(Api.ApiMarker).Assembly;
}

public class LayerDependencyTests
{
    [Fact]
    public void DomainShouldNotDependOnOtherLayers()
    {
        var result = Types.InAssembly(Layers.Domain)
            .Should()
            .NotHaveDependencyOnAny(
                Layers.ApplicationNamespace,
                Layers.InfrastructureNamespace,
                Layers.ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void ApplicationShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(Layers.Application)
            .Should()
            .NotHaveDependencyOnAny(
                Layers.InfrastructureNamespace,
                Layers.ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void InfrastructureShouldNotDependOnApi()
    {
        var result = Types.InAssembly(Layers.Infrastructure)
            .Should()
            .NotHaveDependencyOnAny(Layers.ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void ApiShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(Layers.Api)
            .Should()
            .NotHaveDependencyOnAny(Layers.InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

}
