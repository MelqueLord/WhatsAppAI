using WhatsAppAI.Domain.Integrations;
using Xunit;

namespace WhatsAppAI.UnitTests.Integrations;

public class BotConfigurationTests
{
    [Fact]
    public void Create_DefaultModeIsManual()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());

        Assert.Equal(BotMode.Manual, config.Mode);
        Assert.True(config.Enabled);
        Assert.Equal(500, config.MaxTokensPerResponse);
    }

    [Fact]
    public void Create_WithMode_SetsMode()
    {
        var config = BotConfiguration.Create(Guid.NewGuid(), BotMode.AiPowered);

        Assert.Equal(BotMode.AiPowered, config.Mode);
    }

    [Fact]
    public void UpdateMode_ChangesMode()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        config.UpdateMode(BotMode.AiPowered);

        Assert.Equal(BotMode.AiPowered, config.Mode);
    }

    [Fact]
    public void UpdateMode_IncrementsVersion()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        var originalVersion = config.Version;
        config.UpdateMode(BotMode.SimpleAutoReply);

        Assert.Equal(originalVersion + 1, config.Version);
    }

    [Fact]
    public void UpdateMessages_SetsMessages()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        config.UpdateMessages("Welcome", "Offline", "Fallback", "Handoff", "Media");

        Assert.Equal("Welcome", config.WelcomeMessage);
        Assert.Equal("Offline", config.OfflineMessage);
        Assert.Equal("Fallback", config.FallbackMessage);
        Assert.Equal("Handoff", config.HandoffMessage);
        Assert.Equal("Media", config.MediaMessage);
    }

    [Fact]
    public void UpdateMessages_TrimsWhitespace()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        config.UpdateMessages("  Welcome  ", null, "  Fallback  ", null, null);

        Assert.Equal("Welcome", config.WelcomeMessage);
        Assert.Null(config.OfflineMessage);
        Assert.Equal("Fallback", config.FallbackMessage);
        Assert.Null(config.HandoffMessage);
        Assert.Null(config.MediaMessage);
    }

    [Fact]
    public void UpdateTokenLimit_ClampsValue()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        config.UpdateTokenLimit(10);

        Assert.Equal(50, config.MaxTokensPerResponse);
    }

    [Fact]
    public void UpdateTokenLimit_ClampsMax()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        config.UpdateTokenLimit(5000);

        Assert.Equal(2000, config.MaxTokensPerResponse);
    }

    [Fact]
    public void Toggle_DisablesBot()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        config.Toggle(false);

        Assert.False(config.Enabled);
    }

    [Fact]
    public void Toggle_EnablesBot()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        config.Toggle(false);
        config.Toggle(true);

        Assert.True(config.Enabled);
    }
}
