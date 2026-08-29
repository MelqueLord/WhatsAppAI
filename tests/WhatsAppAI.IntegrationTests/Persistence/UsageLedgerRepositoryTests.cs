using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Usage;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Persistence;

[Collection("IntegrationTests")]
public sealed class UsageLedgerRepositoryTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task Monthly_totals_are_scoped_by_tenant_and_metric()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IUsageLedgerRepository>();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await repository.AddAsync(UsageLedger.Create(
            tenantA, "OpenAI", UsageMetricNames.AiResponses, $"reply-{Guid.NewGuid()}", 3, "responses"));
        await repository.AddAsync(UsageLedger.Create(
            tenantB, "OpenAI", UsageMetricNames.AiResponses, $"reply-{Guid.NewGuid()}", 7, "responses"));
        await repository.AddAsync(UsageLedger.Create(
            tenantA, "OpenAI", "output_tokens", $"tokens-{Guid.NewGuid()}", 999, "tokens"));

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var totals = await repository.GetTotalQuantityByTenantAsync(
            UsageMetricNames.AiResponses, monthStart, monthStart.AddMonths(1));

        Assert.Equal(3, await repository.GetTotalQuantityAsync(
            tenantA, UsageMetricNames.AiResponses, monthStart, monthStart.AddMonths(1)));
        Assert.Equal(3, totals[tenantA]);
        Assert.Equal(7, totals[tenantB]);
    }
}
