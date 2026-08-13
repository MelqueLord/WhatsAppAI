using Xunit;

namespace WhatsAppAI.IntegrationTests.Realtime;

public class SignalRSecurityTests
{
    [Fact]
    public void InboxHub_ShouldRequireAuthorization()
    {
        var hubType = typeof(WhatsAppAI.WebApi.Hubs.InboxHub);
        var authorizeAttribute = hubType.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);

        Assert.NotEmpty(authorizeAttribute);
    }

    [Fact]
    public void InboxHub_ShouldHaveCorrectMethods()
    {
        var hubType = typeof(WhatsAppAI.WebApi.Hubs.InboxHub);

        Assert.NotNull(hubType.GetMethod("OnConnectedAsync"));
        Assert.NotNull(hubType.GetMethod("OnDisconnectedAsync"));
        Assert.NotNull(hubType.GetMethod("JoinConversation"));
        Assert.NotNull(hubType.GetMethod("LeaveConversation"));
    }

    [Fact]
    public void InboxHubMethods_ShouldHaveCorrectConstants()
    {
        Assert.Equal("NewMessage", WhatsAppAI.WebApi.Hubs.InboxHubMethods.NewMessage);
        Assert.Equal("MessageStatusUpdated", WhatsAppAI.WebApi.Hubs.InboxHubMethods.MessageStatusUpdated);
        Assert.Equal("ConversationUpdated", WhatsAppAI.WebApi.Hubs.InboxHubMethods.ConversationUpdated);
        Assert.Equal("TypingIndicator", WhatsAppAI.WebApi.Hubs.InboxHubMethods.TypingIndicator);
    }
}
