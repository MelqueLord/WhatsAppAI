using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.Application.Automation.Policy;

public static class AiDataProcessingPolicy
{
    public static bool IsAuthorized(
        Guid tenantId,
        Guid contactId,
        IReadOnlyCollection<ProcessingPurpose> purposes,
        IReadOnlyCollection<ConsentEvidence> consents)
    {
        var applicablePurposes = purposes
            .Where(purpose => purpose.TenantId == tenantId && purpose.IsActive)
            .ToList();

        if (applicablePurposes.Exists(purpose => purpose.LegalBasis != LegalBasis.Consent))
            return true;

        var consentPurposeIds = applicablePurposes
            .Where(purpose => purpose.LegalBasis == LegalBasis.Consent)
            .Select(purpose => purpose.Id)
            .ToHashSet();

        return consents.Any(consent =>
            consent.TenantId == tenantId &&
            consent.ContactId == contactId &&
            consent.RevokedAt is null &&
            consentPurposeIds.Contains(consent.ProcessingPurposeId));
    }
}
