using WhatsAppAI.Application.Broadcast;
using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.UnitTests.Broadcast;

public sealed class MarketingBroadcastConsentPolicyTests
{
    [Theory]
    [InlineData("MARKETING")]
    [InlineData("marketing")]
    public void IsMarketing_AcceptsMarketingCategory(string category)
    {
        Assert.True(MarketingBroadcastConsentPolicy.IsMarketing(category));
    }

    [Theory]
    [InlineData("UTILITY")]
    [InlineData(null)]
    public void IsMarketing_RejectsOtherCategories(string? category)
    {
        Assert.False(MarketingBroadcastConsentPolicy.IsMarketing(category));
    }

    [Fact]
    public void HasActiveConsent_RejectsRevokedConsentAndOtherTenantEvidence()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purpose = ProcessingPurpose.Create(
            tenantId,
            MarketingBroadcastConsentPolicy.PurposeName,
            MarketingBroadcastConsentPolicy.PurposeDescription,
            LegalBasis.Consent,
            3650,
            Guid.NewGuid());
        var active = ConsentEvidence.Create(tenantId, contactId, purpose, "test", null, DateTime.UtcNow, Guid.NewGuid());
        var revoked = ConsentEvidence.Create(tenantId, contactId, purpose, "test", null, DateTime.UtcNow, Guid.NewGuid());
        revoked.Revoke(DateTime.UtcNow);
        var otherTenant = ConsentEvidence.Create(tenantId, Guid.NewGuid(), purpose, "test", null, DateTime.UtcNow, Guid.NewGuid());

        Assert.True(MarketingBroadcastConsentPolicy.HasActiveConsent(tenantId, contactId, [purpose], [active, revoked, otherTenant]));

        active.Revoke(DateTime.UtcNow);

        Assert.False(MarketingBroadcastConsentPolicy.HasActiveConsent(tenantId, contactId, [purpose], [active, revoked, otherTenant]));
    }
}
