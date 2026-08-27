using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Privacy;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiDataProcessingPolicyTests
{
    [Fact]
    public void IsAuthorized_RejectsWithoutAnActivePurpose()
    {
        Assert.False(AiDataProcessingPolicy.IsAuthorized(Guid.NewGuid(), Guid.NewGuid(), [], []));
    }

    [Fact]
    public void IsAuthorized_AcceptsTenantPurposeWithNonConsentLegalBasis()
    {
        var tenantId = Guid.NewGuid();
        var purpose = ProcessingPurpose.Create(tenantId, "Support", "Customer support", LegalBasis.Contract, 365, Guid.NewGuid());

        Assert.True(AiDataProcessingPolicy.IsAuthorized(tenantId, Guid.NewGuid(), [purpose], []));
    }

    [Fact]
    public void IsAuthorized_RequiresCurrentConsentForConsentPurpose()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purpose = ProcessingPurpose.Create(tenantId, "AI support", "AI customer support", LegalBasis.Consent, 365, Guid.NewGuid());
        var consent = ConsentEvidence.Create(tenantId, contactId, purpose, "whatsapp", null, DateTime.UtcNow, Guid.NewGuid());

        Assert.True(AiDataProcessingPolicy.IsAuthorized(tenantId, contactId, [purpose], [consent]));

        consent.Revoke(DateTime.UtcNow);
        Assert.False(AiDataProcessingPolicy.IsAuthorized(tenantId, contactId, [purpose], [consent]));
    }
}
