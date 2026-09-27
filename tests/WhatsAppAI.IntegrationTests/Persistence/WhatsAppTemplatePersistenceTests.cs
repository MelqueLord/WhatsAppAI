using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Persistence;

[Collection("IntegrationTests")]
public sealed class WhatsAppTemplatePersistenceTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task Templates_are_scoped_to_their_tenant_waba()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IWhatsAppTemplateRepository>();
        await context.Database.MigrateAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var accountA = WhatsAppAccount.Create(tenantA, $"waba-a-{Guid.NewGuid():N}", $"phone-a-{Guid.NewGuid():N}", "secret-a");
        var accountB = WhatsAppAccount.Create(tenantB, $"waba-b-{Guid.NewGuid():N}", $"phone-b-{Guid.NewGuid():N}", "secret-b");
        await context.WhatsAppAccounts.AddRangeAsync(accountA, accountB);
        await context.SaveChangesAsync();

        var wabaA = await repository.EnsureBusinessAccountAsync(accountA);
        var wabaB = await repository.EnsureBusinessAccountAsync(accountB);
        await repository.AddOrUpdateFromProviderAsync(tenantA, wabaA.Id, Snapshot("meta-a"));
        await repository.AddOrUpdateFromProviderAsync(tenantB, wabaB.Id, Snapshot("meta-b"));

        var templatesA = await repository.ListTemplatesAsync(tenantA, wabaA.Id, 0, 10);
        var templatesB = await repository.ListTemplatesAsync(tenantB, wabaB.Id, 0, 10);

        Assert.Single(templatesA);
        Assert.Equal("meta-a", templatesA[0].MetaTemplateId);
        Assert.Single(templatesB);
        Assert.Equal("meta-b", templatesB[0].MetaTemplateId);
        Assert.Empty(await repository.ListTemplatesAsync(tenantA, wabaB.Id, 0, 10));
    }

    [Fact]
    public async Task A_waba_cannot_be_assigned_to_another_tenant()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IWhatsAppTemplateRepository>();
        await context.Database.MigrateAsync();

        var wabaId = $"waba-shared-{Guid.NewGuid():N}";
        var accountA = WhatsAppAccount.Create(Guid.NewGuid(), wabaId, $"phone-a-{Guid.NewGuid():N}", "secret-a");
        var accountB = WhatsAppAccount.Create(Guid.NewGuid(), wabaId, $"phone-b-{Guid.NewGuid():N}", "secret-b");
        await context.WhatsAppAccounts.AddRangeAsync(accountA, accountB);
        await context.SaveChangesAsync();

        await repository.EnsureBusinessAccountAsync(accountA);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.EnsureBusinessAccountAsync(accountB));
    }

    private static WhatsAppTemplateSummary Snapshot(string metaTemplateId) => new(
        "atualizacao_atendimento", "pt_BR", 1, "UTILITY", "APPROVED", true)
    {
        MetaTemplateId = metaTemplateId,
        BodyText = "Olá, {{1}}.",
        ComponentsJson = "[{\"type\":\"BODY\"}]"
    };
}
