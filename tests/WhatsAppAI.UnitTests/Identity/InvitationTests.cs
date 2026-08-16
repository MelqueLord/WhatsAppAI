using WhatsAppAI.Domain.Identity;
using Xunit;

namespace WhatsAppAI.UnitTests.Identity;

public class InvitationTests
{
    [Fact]
    public void Create_SetsStatusPending()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Equal(InvitationPurpose.TenantOwner, invitation.Purpose);
    }

    [Fact]
    public void Create_SetsExpiration24HoursAhead()
    {
        var before = DateTime.UtcNow;
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.Operator, Guid.NewGuid());

        Assert.True(invitation.ExpiresAt > before.AddHours(23));
        Assert.True(invitation.ExpiresAt <= before.AddHours(24).AddSeconds(5));
    }

    [Fact]
    public void Create_NormalizesEmail()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "  Test@Example.COM  ", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());

        Assert.Equal("test@example.com", invitation.Email);
    }

    [Fact]
    public void IsUsable_ReturnsTrueWhenPending()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());

        Assert.True(invitation.IsUsable);
    }

    [Fact]
    public void IsUsable_ReturnsFalseWhenConsumed()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());
        invitation.Consume();

        Assert.False(invitation.IsUsable);
    }

    [Fact]
    public void IsUsable_ReturnsFalseWhenRevoked()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());
        invitation.Revoke(Guid.NewGuid());

        Assert.False(invitation.IsUsable);
    }

    [Fact]
    public void Consume_SetsStatusConsumed()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());
        invitation.Consume();

        Assert.Equal(InvitationStatus.Consumed, invitation.Status);
        Assert.NotNull(invitation.ConsumedAt);
    }

    [Fact]
    public void Consume_IncrementsVersion()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());
        invitation.Consume();

        Assert.Equal(1u, invitation.Version);
    }

    [Fact]
    public void Consume_ThrowsWhenNotUsable()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());
        invitation.Revoke(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => invitation.Consume());
    }

    [Fact]
    public void Revoke_SetsStatusRevoked()
    {
        var revokedBy = Guid.NewGuid();
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());
        invitation.Revoke(revokedBy);

        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
        Assert.NotNull(invitation.RevokedAt);
        Assert.Equal(revokedBy, invitation.RevokedByUserId);
    }

    [Fact]
    public void Revoke_ThrowsWhenNotPending()
    {
        var invitation = Invitation.Create(
            Guid.NewGuid(), "test@example.com", "token-hash",
            InvitationPurpose.TenantOwner, Guid.NewGuid());
        invitation.Consume();

        Assert.Throws<InvalidOperationException>(() => invitation.Revoke(Guid.NewGuid()));
    }
}
