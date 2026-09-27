using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class WhatsAppTemplateCatalogSyncService(
    IWhatsAppTemplateRepository templateRepository,
    ISecretStore secretStore,
    IWhatsAppClientResolver clientResolver)
{
    public async Task<WhatsAppTemplateListResult> SynchronizeAsync(
        WhatsAppAccount account,
        CancellationToken cancellationToken = default)
    {
        if (account.ConnectionType != WhatsAppConnectionType.OfficialApi || !account.IsActive)
            return new WhatsAppTemplateListResult { ErrorMessage = "A linha oficial não está ativa." };

        var waba = await templateRepository.EnsureBusinessAccountAsync(account, cancellationToken);
        var token = await secretStore.GetAsync(account.AccessTokenRef, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return new WhatsAppTemplateListResult { ErrorMessage = "A credencial da linha não está disponível." };

        var result = await clientResolver.GetClient(WhatsAppConnectionType.OfficialApi)
            .ListTemplatesAsync(waba.WabaId, token, cancellationToken);
        if (!result.IsSuccess)
            return result;

        foreach (var snapshot in result.Templates)
            await templateRepository.AddOrUpdateFromProviderAsync(account.TenantId, waba.Id, snapshot, cancellationToken);

        return result;
    }
}
