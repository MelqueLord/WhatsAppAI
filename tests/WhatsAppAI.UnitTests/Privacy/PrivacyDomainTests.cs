using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.UnitTests.Privacy;

public sealed class PrivacyDomainTests
{
    [Fact]
    public void ConsentEvidence_RejectsNonConsentPurpose()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var purpose = ProcessingPurpose.Create(
            tenantId, "Support", "Customer support", LegalBasis.Contract, 365, userId);

        Assert.Throws<InvalidOperationException>(() => ConsentEvidence.Create(
            tenantId, Guid.NewGuid(), purpose, "WhatsApp", null, DateTime.UtcNow, userId));
    }

    [Fact]
    public void ConsentEvidence_Revoke_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grantedAt = DateTime.UtcNow.AddMinutes(-1);
        var purpose = ProcessingPurpose.Create(
            tenantId, "Marketing", "Marketing messages", LegalBasis.Consent, 180, userId);
        var evidence = ConsentEvidence.Create(
            tenantId, Guid.NewGuid(), purpose, "WhatsApp", "msg-1", grantedAt, userId);
        var firstRevocation = DateTime.UtcNow;

        evidence.Revoke(firstRevocation);
        evidence.Revoke(firstRevocation.AddMinutes(1));

        Assert.Equal(firstRevocation, evidence.RevokedAt);
    }

    [Fact]
    public void ContactAndMessage_Redaction_RemovesPersonalData()
    {
        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(tenantId, "+5511999999999", "Person");
        contact.UpdateProfilePicture("https://example.test/person.jpg");
        var conversation = Conversation.Create(tenantId, contact.Id, "line");
        var message = Message.CreateInbound(
            tenantId, conversation.Id, contact.Id, "external", MessageType.Text, "personal", "media", "caption");

        contact.Anonymize();
        message.RedactPersonalData();

        Assert.StartsWith("anon-", contact.PhoneNumber);
        Assert.Null(contact.Name);
        Assert.Null(contact.ProfilePictureUrl);
        Assert.Null(message.ExternalId);
        Assert.Null(message.Content);
        Assert.Null(message.MediaId);
        Assert.Null(message.Caption);
    }
}
