using System.Text.Json;
using System.Globalization;

namespace WhatsAppAI.Domain.Integrations;

public sealed record BusinessHoursDay(int DayOfWeek, bool Enabled, string Open, string Close);

public static class BusinessHoursPolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> SupportedTimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        "UTC", "America/Sao_Paulo", "America/New_York", "Europe/Lisbon"
    };

    public static bool TryValidate(bool enabled, string? timeZoneId, string? json, out string error)
    {
        error = string.Empty;
        if (!enabled)
            return true;

        if (string.IsNullOrWhiteSpace(timeZoneId) || !SupportedTimeZones.Contains(timeZoneId.Trim()))
        {
            error = "Selecione um fuso horário válido.";
            return false;
        }

        List<BusinessHoursDay>? days;
        try
        {
            days = JsonSerializer.Deserialize<List<BusinessHoursDay>>(json ?? string.Empty, JsonOptions);
        }
        catch (JsonException)
        {
            error = "Configure os horários dos dias da semana.";
            return false;
        }

        if (days is null || days.Count != 7 || days.Select(day => day.DayOfWeek).Distinct().Count() != 7 ||
            days.Exists(day => day.DayOfWeek is < 0 or > 6))
        {
            error = "Configure exatamente um horário para cada dia da semana.";
            return false;
        }

        foreach (var day in days)
        {
            if (!day.Enabled)
                continue;

            if (!TimeOnly.TryParse(day.Open, CultureInfo.InvariantCulture, DateTimeStyles.None, out var open) ||
                !TimeOnly.TryParse(day.Close, CultureInfo.InvariantCulture, DateTimeStyles.None, out var close) || open >= close)
            {
                error = "Os horários de abertura e fechamento devem ser válidos e diferentes.";
                return false;
            }
        }

        return true;
    }

    public static bool IsOpen(bool enabled, string? json, string timeZoneId, DateTime utcNow)
    {
        if (!enabled)
            return true;

        if (!TryValidate(true, timeZoneId, json, out _))
            return false;

        var days = JsonSerializer.Deserialize<List<BusinessHoursDay>>(json!, JsonOptions)!;
        var zone = ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var day = days.Single(item => item.DayOfWeek == (int)local.DayOfWeek);
        return day.Enabled && local.TimeOfDay >= TimeSpan.Parse(day.Open, CultureInfo.InvariantCulture) &&
            local.TimeOfDay < TimeSpan.Parse(day.Close, CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("America/Sao_Paulo", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("America/New_York", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Europe/Lisbon", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
    }
}
