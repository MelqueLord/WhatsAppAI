using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Usage;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Persistence;

[Collection("IntegrationTests")]
public sealed class AiResponseQuotaServiceTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task Reservation_is_idempotent_and_never_exceeds_tenant_limit()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        var plan = await dbContext.SubscriptionPlans.FirstAsync();
        var tenant = Tenant.Create(
            $"Quota {Guid.NewGuid():N}",
            $"quota-{Guid.NewGuid():N}",
            plan.Id,
            monthlyAiResponseLimit: 1);
        tenant.Activate();
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IAiResponseQuotaService>();
        var firstMessageId = Guid.NewGuid();
        var first = await service.TryReserveAsync(
            tenant.Id, firstMessageId, "message:first");
        var repeated = await service.TryReserveAsync(
            tenant.Id, firstMessageId, "message:first");
        var second = await service.TryReserveAsync(
            tenant.Id, Guid.NewGuid(), "message:second");

        Assert.True(first.IsReserved);
        Assert.False(first.IsExisting);
        Assert.Equal(first.ReservationId, repeated.ReservationId);
        Assert.True(repeated.IsExisting);
        Assert.True(repeated.IsReserved);
        Assert.False(second.IsReserved);
        Assert.Equal(0, second.Snapshot.AvailableResponses);
    }

    [Fact]
    public async Task Reconciliation_releases_only_expired_pending_reservations()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        var plan = await dbContext.SubscriptionPlans.FirstAsync();
        var tenant = Tenant.Create(
            $"Reconciliation {Guid.NewGuid():N}",
            $"reconciliation-{Guid.NewGuid():N}",
            plan.Id,
            monthlyAiResponseLimit: 2);
        tenant.Activate();
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var quotaService = scope.ServiceProvider.GetRequiredService<IAiResponseQuotaService>();
        var expired = await quotaService.TryReserveAsync(tenant.Id, Guid.NewGuid(), "reconcile:expired");
        var recent = await quotaService.TryReserveAsync(tenant.Id, Guid.NewGuid(), "reconcile:recent");
        Assert.True(expired.IsReserved);
        Assert.True(recent.IsReserved);

        var expiredReservation = await dbContext.AiResponseQuotaReservations
            .IgnoreQueryFilters()
            .SingleAsync(reservation => reservation.Id == expired.ReservationId);
        dbContext.Entry(expiredReservation).Property(reservation => reservation.CreatedAt).CurrentValue =
            DateTime.UtcNow.AddMinutes(-20);
        await dbContext.SaveChangesAsync();

        var reconciler = scope.ServiceProvider.GetRequiredService<IAiResponseQuotaReconciler>();
        var result = await reconciler.ReconcileAsync(
            DateTime.UtcNow,
            TimeSpan.FromMinutes(10),
            batchSize: 50);

        Assert.Equal(1, result.ExaminedCount);
        Assert.Equal(1, result.ReleasedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(
            AiResponseQuotaReservationStatus.Released,
            await dbContext.AiResponseQuotaReservations
                .IgnoreQueryFilters()
                .Where(reservation => reservation.Id == expired.ReservationId)
                .Select(reservation => reservation.Status)
                .SingleAsync());
        Assert.Equal(
            AiResponseQuotaReservationStatus.Pending,
            await dbContext.AiResponseQuotaReservations
                .IgnoreQueryFilters()
                .Where(reservation => reservation.Id == recent.ReservationId)
                .Select(reservation => reservation.Status)
                .SingleAsync());
    }
}
