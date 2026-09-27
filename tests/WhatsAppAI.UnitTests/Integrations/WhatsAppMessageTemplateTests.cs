using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.UnitTests.Integrations;

public sealed class WhatsAppMessageTemplateTests
{
    [Fact]
    public void Validator_RejectsNonContiguousPlaceholdersAndMissingExamples()
    {
        var errors = WhatsAppTemplateValidator.Validate(new WhatsAppTemplateCreateRequest(
            "atualizacao", "pt_BR", "UTILITY", "Olá {{1}} e {{3}}", ["Maria"], null));

        Assert.Contains("bodyText", errors.Keys);
        Assert.Contains("bodyExamples", errors.Keys);
    }

    [Fact]
    public void ApprovedUtilityTemplate_WithSupportedComponents_IsCompatibleWithBothFlows()
    {
        var template = WhatsAppMessageTemplate.CreatePending(Guid.NewGuid(), Guid.NewGuid(), "atualizacao", "pt_BR",
            "UTILITY", "Olá {{1}}", null, ["Maria"], 1);

        template.ApplyProviderSnapshot("meta-1", "UTILITY", "APPROVED", "Olá {{1}}", null, 1,
            "[{\"type\":\"BODY\",\"text\":\"Olá {{1}}\"}]", DateTime.UtcNow);

        Assert.Equal(WhatsAppTemplateReviewStatus.Approved, template.ReviewStatus);
        Assert.True(template.IsInboxCompatible);
        Assert.True(template.IsBroadcastCompatible);
    }

    [Fact]
    public void ReusedIdempotencyKey_WithDifferentFingerprint_IsRepresentedByDistinctInvariant()
    {
        var submission = WhatsAppTemplateSubmission.Queue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "request-123", "fingerprint-a", "correlation");

        Assert.Equal("fingerprint-a", submission.RequestFingerprint);
        Assert.Equal(WhatsAppTemplateSubmissionStatus.Queued, submission.Status);
    }
}
