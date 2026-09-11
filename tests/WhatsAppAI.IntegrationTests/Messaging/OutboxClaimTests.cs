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

    [Fact]
    public async Task RecoverStaleClaims_ReturnsAbandonedProcessingItemToPending()
    {
        Guid outboxId;
        await using (var db = await factory.GetDbContextAsync())
        {
            var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
            db.OutboxMessages.Add(outbox);
            await db.SaveChangesAsync();
            outboxId = outbox.Id;
        }

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var claimed = await repository.TryClaimAsync(outboxId, DateTime.UtcNow.AddMinutes(-10));

        Assert.True(claimed);
        var recovered = await repository.RecoverStaleClaimsAsync(DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal(1, recovered);

        await using var verifyDb = await factory.GetDbContextAsync();
        var recoveredOutbox = await verifyDb.OutboxMessages
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == outboxId);
        Assert.Equal(OutboxStatus.Pending, recoveredOutbox.Status);
        Assert.Null(recoveredOutbox.NextRetryAt);
    }
}
