using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Application.Abstractions;

public interface IWhatsAppTemplateRepository
{
    Task<WhatsAppBusinessAccount?> GetBusinessAccountAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<WhatsAppBusinessAccount?> GetBusinessAccountByExternalIdAsync(string wabaId, CancellationToken cancellationToken = default);
    Task<WhatsAppBusinessAccount> EnsureBusinessAccountAsync(WhatsAppAccount account, CancellationToken cancellationToken = default);
    Task<WhatsAppMessageTemplate?> GetTemplateAsync(Guid tenantId, Guid wabaId, string name, string language, CancellationToken cancellationToken = default);
    Task<WhatsAppMessageTemplate?> GetTemplateByMetaIdAsync(Guid tenantId, Guid wabaId, string metaTemplateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WhatsAppMessageTemplate>> ListTemplatesAsync(Guid tenantId, Guid wabaId, int skip, int limit, CancellationToken cancellationToken = default);
    Task<WhatsAppTemplateSubmission?> GetSubmissionByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task AddTemplateAndSubmissionAsync(WhatsAppMessageTemplate template, WhatsAppTemplateSubmission submission, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WhatsAppTemplateSubmission>> GetDueSubmissionsAsync(int limit, DateTime now, CancellationToken cancellationToken = default);
    Task<WhatsAppMessageTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WhatsAppAccount?> GetSourceAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task AddOrUpdateFromProviderAsync(Guid tenantId, Guid wabaId, WhatsAppTemplateSummary snapshot, CancellationToken cancellationToken = default);
}
