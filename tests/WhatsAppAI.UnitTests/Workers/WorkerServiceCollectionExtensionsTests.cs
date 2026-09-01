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

        var hostedWorkers = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .ToList();

        Assert.Collection(
            hostedWorkers,
            descriptor => Assert.Equal(typeof(WebhookProcessingWorker), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(OutboxProcessingWorker), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(AiOrchestrationWorker), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(AiResponseQuotaReconciliationWorker), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(RetentionWorker), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(TenantSuspensionWorker), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(BroadcastDispatchWorker), descriptor.ImplementationType));
    }
}
