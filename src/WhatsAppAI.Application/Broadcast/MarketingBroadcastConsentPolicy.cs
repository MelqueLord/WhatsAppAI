using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.Application.Broadcast;

public static class MarketingBroadcastConsentPolicy
{
    public const string PurposeName = "Comunicações de marketing por WhatsApp";
    public const string PurposeDescription =
        "Envio de mensagens de marketing por WhatsApp mediante autorização explícita do contato.";
    public const string OperatorConfirmedSource = "tenant-owner-confirmed";

    public static bool IsMarketing(string? category) =>
        string.Equals(category, "MARKETING", StringComparison.OrdinalIgnoreCase);

    public static bool HasActiveConsent(
        Guid tenantId,
        Guid contactId,
        IReadOnlyCollection<ProcessingPurpose> purposes,
        IReadOnlyCollection<ConsentEvidence> consents)
    {
        var marketingPurposeIds = purposes
            .Where(purpose =>
                purpose.TenantId == tenantId &&
                purpose.IsActive &&
                purpose.LegalBasis == LegalBasis.Consent &&
                purpose.Name == PurposeName)
            .Select(purpose => purpose.Id)
            .ToHashSet();

        return consents.Any(consent =>
            consent.TenantId == tenantId &&
            consent.ContactId == contactId &&
            consent.RevokedAt is null &&
            marketingPurposeIds.Contains(consent.ProcessingPurposeId));
    }
}
