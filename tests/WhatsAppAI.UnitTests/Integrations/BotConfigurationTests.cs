using WhatsAppAI.Domain.Integrations;
using System.Text.Json;
using Xunit;

namespace WhatsAppAI.UnitTests.Integrations;

public class BotConfigurationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    [Fact]
    public void Create_DefaultModeIsManual()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());

        Assert.Equal(BotMode.Manual, config.Mode);
        Assert.True(config.Enabled);
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
        config.UpdateMessages("Welcome", "Offline", "Fallback", "Handoff", "QueueTransfer", "Media");

        Assert.Equal("Welcome", config.WelcomeMessage);
        Assert.Equal("Offline", config.OfflineMessage);
        Assert.Equal("Fallback", config.FallbackMessage);
        Assert.Equal("Handoff", config.HandoffMessage);
        Assert.Equal("QueueTransfer", config.QueueTransferMessage);
        Assert.Equal("Media", config.MediaMessage);
    }

    [Fact]
    public void UpdateMessages_TrimsWhitespace()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        config.UpdateMessages("  Welcome  ", null, "  Fallback  ", null, null, null);

        Assert.Equal("Welcome", config.WelcomeMessage);
        Assert.Null(config.OfflineMessage);
        Assert.Equal("Fallback", config.FallbackMessage);
        Assert.Null(config.HandoffMessage);
        Assert.Null(config.QueueTransferMessage);
        Assert.Null(config.MediaMessage);
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

    [Fact]
    public void UpdateBusinessHours_PersistsScheduleAndTimezone()
    {
        var config = BotConfiguration.Create(Guid.NewGuid());
        var schedule = JsonSerializer.Serialize(DefaultSchedule(), JsonOptions);

        config.UpdateBusinessHours(true, "America/Sao_Paulo", schedule);

        Assert.True(config.BusinessHoursEnabled);
        Assert.Equal("America/Sao_Paulo", config.TimeZoneId);
        Assert.Equal(schedule, config.BusinessHoursJson);
    }

    [Fact]
    public void BusinessHoursPolicy_UsesConfiguredTimezoneAndDay()
    {
        var schedule = JsonSerializer.Serialize(DefaultSchedule(), JsonOptions);

        Assert.True(BusinessHoursPolicy.IsOpen(true, schedule, "America/Sao_Paulo", new DateTime(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc)));
        Assert.False(BusinessHoursPolicy.IsOpen(true, schedule, "America/Sao_Paulo", new DateTime(2026, 8, 28, 22, 0, 0, DateTimeKind.Utc)));
        Assert.True(BusinessHoursPolicy.IsOpen(false, null, "America/Sao_Paulo", DateTime.UtcNow));
    }

    private static BusinessHoursDay[] DefaultSchedule() =>
        Enumerable.Range(0, 7)
            .Select(day => new BusinessHoursDay(day, day is >= 1 and <= 5, "09:00", "18:00"))
            .ToArray();
}
