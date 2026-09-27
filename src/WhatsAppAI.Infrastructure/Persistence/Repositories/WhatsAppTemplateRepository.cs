using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class WhatsAppTemplateRepository(AppDbContext context) : IWhatsAppTemplateRepository
{
    public Task<WhatsAppBusinessAccount?> GetBusinessAccountAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) =>
        context.WhatsAppBusinessAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<WhatsAppBusinessAccount?> GetBusinessAccountByExternalIdAsync(string wabaId, CancellationToken cancellationToken = default) =>
        context.WhatsAppBusinessAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.WabaId == wabaId, cancellationToken);

    public async Task<WhatsAppBusinessAccount> EnsureBusinessAccountAsync(WhatsAppAccount account, CancellationToken cancellationToken = default)
    {
        if (account.WhatsAppBusinessAccountId is Guid currentId)
            return await GetBusinessAccountAsync(account.TenantId, currentId, cancellationToken)
                ?? throw new InvalidOperationException("A WABA vinculada à linha não existe.");

        var existing = await context.WhatsAppBusinessAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.WabaId == account.WabaId, cancellationToken);
        if (existing is not null && existing.TenantId != account.TenantId)
            throw new InvalidOperationException("A WABA já pertence a outro tenant.");
        existing ??= WhatsAppBusinessAccount.Create(account.TenantId, account.WabaId);
        if (context.Entry(existing).State == EntityState.Detached) await context.WhatsAppBusinessAccounts.AddAsync(existing, cancellationToken);
        account.AssignBusinessAccount(existing.Id);
        await context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public Task<WhatsAppMessageTemplate?> GetTemplateAsync(Guid tenantId, Guid wabaId, string name, string language, CancellationToken cancellationToken = default) =>
        context.WhatsAppMessageTemplates.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WhatsAppBusinessAccountId == wabaId && x.Name == name && x.Language == language, cancellationToken);

    public Task<WhatsAppMessageTemplate?> GetTemplateByMetaIdAsync(Guid tenantId, Guid wabaId, string metaTemplateId, CancellationToken cancellationToken = default) =>
        context.WhatsAppMessageTemplates.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WhatsAppBusinessAccountId == wabaId && x.MetaTemplateId == metaTemplateId, cancellationToken);

    public async Task<IReadOnlyList<WhatsAppMessageTemplate>> ListTemplatesAsync(Guid tenantId, Guid wabaId, int skip, int limit, CancellationToken cancellationToken = default) =>
        await context.WhatsAppMessageTemplates.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.WhatsAppBusinessAccountId == wabaId)
            .OrderBy(x => x.Name).ThenBy(x => x.Language).ThenBy(x => x.Id)
            .Skip(Math.Max(skip, 0)).Take(Math.Clamp(limit, 1, 101)).ToListAsync(cancellationToken);

    public Task<WhatsAppTemplateSubmission?> GetSubmissionByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        context.WhatsAppTemplateSubmissions.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddTemplateAndSubmissionAsync(WhatsAppMessageTemplate template, WhatsAppTemplateSubmission submission, CancellationToken cancellationToken = default)
    {
        await context.WhatsAppMessageTemplates.AddAsync(template, cancellationToken);
        await context.WhatsAppTemplateSubmissions.AddAsync(submission, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WhatsAppTemplateSubmission>> GetDueSubmissionsAsync(int limit, DateTime now, CancellationToken cancellationToken = default) =>
        await context.WhatsAppTemplateSubmissions.IgnoreQueryFilters()
            .Where(x => (x.Status == WhatsAppTemplateSubmissionStatus.Queued || x.Status == WhatsAppTemplateSubmissionStatus.RetryScheduled || x.Status == WhatsAppTemplateSubmissionStatus.OutcomeUnknown) &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now) && (x.ClaimExpiresAt == null || x.ClaimExpiresAt <= now))
            .OrderBy(x => x.CreatedAt).Take(limit).ToListAsync(cancellationToken);

    public Task<WhatsAppMessageTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.WhatsAppMessageTemplates.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<WhatsAppAccount?> GetSourceAccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.WhatsAppAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);

    public async Task AddOrUpdateFromProviderAsync(Guid tenantId, Guid wabaId, WhatsAppTemplateSummary snapshot, CancellationToken cancellationToken = default)
    {
        var entity = !string.IsNullOrWhiteSpace(snapshot.MetaTemplateId)
            ? await GetTemplateByMetaIdAsync(tenantId, wabaId, snapshot.MetaTemplateId, cancellationToken)
            : null;
        entity ??= await GetTemplateAsync(tenantId, wabaId, snapshot.Name, snapshot.Language, cancellationToken);
        if (entity is null)
        {
            entity = WhatsAppMessageTemplate.CreatePending(tenantId, wabaId, snapshot.Name, snapshot.Language,
                snapshot.Category, snapshot.BodyText, snapshot.FooterText, [], snapshot.BodyParameterCount);
            await context.WhatsAppMessageTemplates.AddAsync(entity, cancellationToken);
        }
        entity.ApplyProviderSnapshot(snapshot.MetaTemplateId, snapshot.Category, snapshot.Status, snapshot.BodyText,
            snapshot.FooterText, snapshot.BodyParameterCount, snapshot.ComponentsJson, DateTime.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
    }
}
