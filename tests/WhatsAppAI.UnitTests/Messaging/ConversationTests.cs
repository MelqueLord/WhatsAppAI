using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Messaging;
using Xunit;

namespace WhatsAppAI.UnitTests.Messaging;

public class ConversationTests
{
    [Fact]
    public void Create_DefaultModeIsAutomatic()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");

        Assert.Equal(ConversationMode.Automatic, conversation.Mode);
        Assert.Equal(ConversationStatus.Open, conversation.Status);
        Assert.Equal(1u, conversation.Version);
    }

    [Fact]
    public void Create_WithMode_SetsMode()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123", ConversationMode.Human);

        Assert.Equal(ConversationMode.Human, conversation.Mode);
    }

    [Fact]
    public void SwitchMode_ChangesMode()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        conversation.SwitchMode(ConversationMode.Human, 1, "user123");

        Assert.Equal(ConversationMode.Human, conversation.Mode);
        Assert.Equal("user123", conversation.AssignedToUserId);
    }

    [Fact]
    public void SwitchMode_IncrementsVersion()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        conversation.SwitchMode(ConversationMode.Human, 1);

        Assert.Equal(2u, conversation.Version);
    }

    [Fact]
    public void SwitchMode_ReturnsPreviousMode()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        var previous = conversation.SwitchMode(ConversationMode.Human, 1);

        Assert.Equal(ConversationMode.Automatic, previous);
    }

    [Fact]
    public void AssignQueue_DoesNotAssumeConversation()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");

        conversation.AssignQueue(Guid.NewGuid());

        Assert.Equal(ConversationMode.Automatic, conversation.Mode);
        Assert.Null(conversation.AssignedToUserId);
    }

    [Fact]
    public void SwitchMode_ClearsAssignedUserWhenNotHuman()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        conversation.SwitchMode(ConversationMode.Human, 1, "user123");
        conversation.SwitchMode(ConversationMode.Automatic, 2);

        Assert.Null(conversation.AssignedToUserId);
    }

    [Fact]
    public void SwitchMode_ThrowsOnVersionConflict()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");

        Assert.Throws<ConcurrencyException>(() =>
            conversation.SwitchMode(ConversationMode.Human, 999));
    }

    [Fact]
    public void RenewWindow_SetsExpiry24HoursAhead()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        var before = DateTime.UtcNow;
        conversation.RenewWindow();

        Assert.NotNull(conversation.WindowExpiresAt);
        Assert.True(conversation.WindowExpiresAt.Value > before.AddHours(23));
        Assert.True(conversation.WindowExpiresAt.Value <= before.AddHours(24).AddSeconds(5));
    }

    [Fact]
    public void IsWindowOpen_ReturnsFalseWhenNoExpiry()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");

        Assert.False(conversation.IsWindowOpen(DateTime.UtcNow));
    }

    [Fact]
    public void IsWindowOpen_ReturnsTrueWhenNotExpired()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        conversation.RenewWindow();

        Assert.True(conversation.IsWindowOpen(DateTime.UtcNow));
    }

    [Fact]
    public void IsWindowOpen_ReturnsFalseWhenExpired()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        conversation.RenewWindow();

        Assert.False(conversation.IsWindowOpen(DateTime.UtcNow.AddHours(25)));
    }

    [Fact]
    public void Close_SetsStatusClosed()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        conversation.Close();

        Assert.Equal(ConversationStatus.Closed, conversation.Status);
    }

    [Fact]
    public void Reopen_SetsStatusOpen()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        conversation.Close();
        conversation.Reopen();

        Assert.Equal(ConversationStatus.Open, conversation.Status);
    }

    [Fact]
    public void RecordMessage_UpdatesLastMessageAt()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "phone123");
        var before = DateTime.UtcNow;
        conversation.RecordMessage();

        Assert.NotNull(conversation.LastMessageAt);
        Assert.True(conversation.LastMessageAt.Value >= before);
    }
}
