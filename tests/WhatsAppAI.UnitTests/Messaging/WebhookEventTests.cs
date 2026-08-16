using WhatsAppAI.Domain.Messaging;
using Xunit;

namespace WhatsAppAI.UnitTests.Messaging;

public class WebhookEventTests
{
    [Fact]
    public void Create_SetsStatusPending()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");

        Assert.Equal(WebhookEventStatus.Pending, webhook.Status);
        Assert.Equal(0, webhook.RetryCount);
    }

    [Fact]
    public void Create_SetsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123", tenantId);

        Assert.Equal(tenantId, webhook.TenantId);
    }

    [Fact]
    public void Create_AllowsNullTenantId()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");

        Assert.Null(webhook.TenantId);
    }

    [Fact]
    public void MarkProcessing_SetsStatusProcessing()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        webhook.MarkProcessing();

        Assert.Equal(WebhookEventStatus.Processing, webhook.Status);
    }

    [Fact]
    public void MarkProcessed_SetsStatusProcessed()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        webhook.MarkProcessed();

        Assert.Equal(WebhookEventStatus.Processed, webhook.Status);
        Assert.NotNull(webhook.ProcessedAt);
    }

    [Fact]
    public void MarkFailed_IncrementsRetryCount()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        webhook.MarkFailed("Error 1");
        webhook.MarkFailed("Error 2");

        Assert.Equal(2, webhook.RetryCount);
        Assert.Equal("Error 2", webhook.ErrorMessage);
    }

    [Fact]
    public void MarkFailed_SetsNextRetryAt()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        var before = DateTime.UtcNow;
        webhook.MarkFailed("Error");

        Assert.NotNull(webhook.NextRetryAt);
        Assert.True(webhook.NextRetryAt.Value > before);
    }

    [Fact]
    public void MarkFailed_After5Retries_SetsDead()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        for (int i = 0; i < 5; i++)
            webhook.MarkFailed($"Error {i}");

        Assert.Equal(WebhookEventStatus.Dead, webhook.Status);
        Assert.Equal(5, webhook.RetryCount);
    }

    [Fact]
    public void MarkDead_SetsStatusDead()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        webhook.MarkDead();

        Assert.Equal(WebhookEventStatus.Dead, webhook.Status);
    }

    [Fact]
    public void ShouldRetry_ReturnsFalseWhenPending()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");

        Assert.False(webhook.ShouldRetry);
    }

    [Fact]
    public void ShouldRetry_ReturnsFalseWhenNextRetryInFuture()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        webhook.MarkFailed("Error");

        Assert.False(webhook.ShouldRetry);
    }

    [Fact]
    public void ShouldRetry_ReturnsFalseAfterMaxRetries()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        for (int i = 0; i < 5; i++)
            webhook.MarkFailed($"Error {i}");

        Assert.False(webhook.ShouldRetry);
    }

    [Fact]
    public void SetEncryptedPayload_UpdatesPayload()
    {
        var webhook = WebhookEvent.Create("phone123", "key123", "{}", "sig123");
        webhook.SetEncryptedPayload("encrypted-data");

        Assert.Equal("encrypted-data", webhook.EncryptedPayload);
    }
}
