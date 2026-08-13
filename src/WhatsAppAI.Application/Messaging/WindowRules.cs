namespace WhatsAppAI.Application.Messaging;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public static class WindowRules
{
    public static bool IsWindowOpen(DateTime? windowExpiresAt, IClock clock)
    {
        return windowExpiresAt.HasValue && windowExpiresAt.Value > clock.UtcNow;
    }

    public static DateTime RenewWindow(IClock clock)
    {
        return clock.UtcNow.AddHours(24);
    }
}
