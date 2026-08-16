using WhatsAppAI.Domain.Identity;
using Xunit;

namespace WhatsAppAI.UnitTests.Identity;

public class UserTests
{
    [Fact]
    public void Create_SetsEmailLowerCase()
    {
        var user = User.Create("  Test@Example.COM  ");

        Assert.Equal("test@example.com", user.Email);
    }

    [Fact]
    public void Create_SetsDisplayName()
    {
        var user = User.Create("test@example.com", "John Doe");

        Assert.Equal("John Doe", user.DisplayName);
    }

    [Fact]
    public void Create_SetsInactive()
    {
        var user = User.Create("test@example.com");

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Create_GeneratesSecurityStamp()
    {
        var user = User.Create("test@example.com");

        Assert.NotNull(user.SecurityStamp);
        Assert.NotEmpty(user.SecurityStamp);
    }

    [Fact]
    public void Activate_SetsActive()
    {
        var user = User.Create("test@example.com");
        user.Activate("hashed-password");

        Assert.True(user.IsActive);
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.NotNull(user.ActivatedAt);
    }

    [Fact]
    public void Activate_RotatesSecurityStamp()
    {
        var user = User.Create("test@example.com");
        var originalStamp = user.SecurityStamp;
        user.Activate("hashed-password");

        Assert.NotEqual(originalStamp, user.SecurityStamp);
    }

    [Fact]
    public void Activate_ThrowsWhenAlreadyActive()
    {
        var user = User.Create("test@example.com");
        user.Activate("hashed-password");

        Assert.Throws<InvalidOperationException>(() => user.Activate("new-hash"));
    }

    [Fact]
    public void Deactivate_SetsInactive()
    {
        var user = User.Create("test@example.com");
        user.Activate("hashed-password");
        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void Deactivate_RotatesSecurityStamp()
    {
        var user = User.Create("test@example.com");
        user.Activate("hashed-password");
        var stampBefore = user.SecurityStamp;
        user.Deactivate();

        Assert.NotEqual(stampBefore, user.SecurityStamp);
    }

    [Fact]
    public void Deactivate_ThrowsWhenAlreadyInactive()
    {
        var user = User.Create("test@example.com");

        Assert.Throws<InvalidOperationException>(() => user.Deactivate());
    }

    [Fact]
    public void RecordLogin_SetsLastLoginAt()
    {
        var user = User.Create("test@example.com");
        user.Activate("hashed-password");
        var before = DateTime.UtcNow;
        user.RecordLogin();

        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt.Value >= before);
    }

    [Fact]
    public void UpdatePassword_ChangesPasswordAndStamp()
    {
        var user = User.Create("test@example.com");
        user.Activate("old-hash");
        var stampBefore = user.SecurityStamp;
        user.UpdatePassword("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.NotEqual(stampBefore, user.SecurityStamp);
    }

    [Fact]
    public void GrantPlatformAdmin_SetsFlag()
    {
        var user = User.Create("test@example.com");
        user.GrantPlatformAdmin();

        Assert.True(user.IsPlatformAdmin);
    }

    [Fact]
    public void RevokePlatformAdmin_ClearsFlag()
    {
        var user = User.Create("test@example.com");
        user.GrantPlatformAdmin();
        user.RevokePlatformAdmin();

        Assert.False(user.IsPlatformAdmin);
    }
}
