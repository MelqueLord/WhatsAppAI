using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Messaging;

[Collection("IntegrationTests")]
public sealed class OutboxClaimTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task OutboxClaimIsAtomicAcrossConcurrentWorkers()
    {
        await using (var db = await factory.GetDbContextAsync())
        {
            db.OutboxMessages.Add(OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        await using var readDb = await factory.GetDbContextAsync();
        var outboxId = await readDb.OutboxMessages
            .IgnoreQueryFilters()
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.Id)
            .FirstAsync();

        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var repository1 = scope1.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var repository2 = scope2.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        var now = DateTime.UtcNow;
        var claims = await Task.WhenAll(
            repository1.TryClaimAsync(outboxId, now),
            repository2.TryClaimAsync(outboxId, now));

        Assert.Single(claims, claimed => claimed);
        Assert.False(claims[0] && claims[1]);
    }
}
