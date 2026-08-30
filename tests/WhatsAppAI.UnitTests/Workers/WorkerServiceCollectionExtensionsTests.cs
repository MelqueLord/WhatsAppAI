using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WhatsAppAI.Infrastructure.Workers;

namespace WhatsAppAI.UnitTests.Workers;

public sealed class WorkerServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWorkers_WhenDisabled_DoesNotRegisterHostedWorkers()
    {
        var services = new ServiceCollection();

        services.AddWorkers(false);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddWorkers_WhenEnabled_RegistersAllHostedWorkers()
    {
        var services = new ServiceCollection();

        services.AddWorkers();

        Assert.Equal(6, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }
}
