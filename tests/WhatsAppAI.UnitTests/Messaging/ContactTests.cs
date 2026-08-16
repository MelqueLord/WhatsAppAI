using WhatsAppAI.Domain.Messaging;
using Xunit;

namespace WhatsAppAI.UnitTests.Messaging;

public class ContactTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(tenantId, "+5511999999999", "John Doe");

        Assert.Equal(tenantId, contact.TenantId);
        Assert.Equal("+5511999999999", contact.PhoneNumber);
        Assert.Equal("John Doe", contact.Name);
    }

    [Fact]
    public void Create_AllowsNullName()
    {
        var contact = Contact.Create(Guid.NewGuid(), "+5511999999999");

        Assert.Null(contact.Name);
    }

    [Fact]
    public void Create_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;
        var contact = Contact.Create(Guid.NewGuid(), "+5511999999999");

        Assert.True(contact.CreatedAt >= before);
        Assert.True(contact.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void UpdateName_ChangesName()
    {
        var contact = Contact.Create(Guid.NewGuid(), "+5511999999999", "John");
        contact.UpdateName("Jane");

        Assert.Equal("Jane", contact.Name);
    }

    [Fact]
    public void UpdateName_DoesNotUpdateWhenSame()
    {
        var contact = Contact.Create(Guid.NewGuid(), "+5511999999999", "John");
        var beforeUpdate = contact.UpdatedAt;
        contact.UpdateName("John");

        Assert.Equal(beforeUpdate, contact.UpdatedAt);
    }

    [Fact]
    public void UpdateName_NullDoesNotChangeName()
    {
        var contact = Contact.Create(Guid.NewGuid(), "+5511999999999", "John");
        contact.UpdateName(null);

        Assert.Equal("John", contact.Name);
    }

    [Fact]
    public void UpdateProfilePicture_SetsUrl()
    {
        var contact = Contact.Create(Guid.NewGuid(), "+5511999999999");
        contact.UpdateProfilePicture("https://example.com/pic.jpg");

        Assert.Equal("https://example.com/pic.jpg", contact.ProfilePictureUrl);
    }

    [Fact]
    public void UpdateProfilePicture_UpdatesTimestamp()
    {
        var contact = Contact.Create(Guid.NewGuid(), "+5511999999999");
        contact.UpdateProfilePicture("https://example.com/pic.jpg");

        Assert.NotNull(contact.UpdatedAt);
    }

    [Fact]
    public void RecordMessage_UpdatesLastMessageAt()
    {
        var contact = Contact.Create(Guid.NewGuid(), "+5511999999999");
        var before = DateTime.UtcNow;
        contact.RecordMessage();

        Assert.NotNull(contact.LastMessageAt);
        Assert.True(contact.LastMessageAt.Value >= before);
    }
}
