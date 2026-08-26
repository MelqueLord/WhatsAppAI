using NetArchTest.Rules;

namespace WhatsAppAI.ArchitectureTests;

public class DependencyTests
{
    private static readonly string DomainNamespace = "WhatsAppAI.Domain";
    private static readonly string ApplicationNamespace = "WhatsAppAI.Application";
    private static readonly string InfrastructureNamespace = "WhatsAppAI.Infrastructure";
    private static readonly string WebApiNamespace = "WhatsAppAI.WebApi";

    [Fact]
    public void Domain_Should_Not_DependOnApplication()
    {
        var result = Types.InAssembly(typeof(WhatsAppAI.Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, $"Domain should not depend on Application. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_Should_Not_DependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(WhatsAppAI.Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, $"Domain should not depend on Infrastructure. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_Should_Not_DependOnWebApi()
    {
        var result = Types.InAssembly(typeof(WhatsAppAI.Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(WebApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, $"Domain should not depend on WebApi. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Should_Not_DependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(WhatsAppAI.Application.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, $"Application should not depend on Infrastructure. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Should_Not_DependOnWebApi()
    {
        var result = Types.InAssembly(typeof(WhatsAppAI.Application.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(WebApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, $"Application should not depend on WebApi. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_Should_Not_DependOnExternalPackages()
    {
        var result = Types.InAssembly(typeof(WhatsAppAI.Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "MediatR",
                "MassTransit",
                "StackExchange.Redis",
                "Newtonsoft.Json",
                "System.Text.Json")
            .GetResult();

        Assert.True(result.IsSuccessful, $"Domain should not depend on external packages. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Should_Not_DependOnExternalPackages()
    {
        var result = Types.InAssembly(typeof(WhatsAppAI.Application.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "StackExchange.Redis",
                "Newtonsoft.Json")
            .GetResult();

        Assert.True(result.IsSuccessful, $"Application should not depend on external packages. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
