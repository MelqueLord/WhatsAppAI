using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace WhatsAppAI.Infrastructure.Observability;

public sealed class SanitizingEnricher : ILogEventEnricher
{
    private static readonly Regex PhonePattern = new(@"\+?\d{10,15}", RegexOptions.Compiled);
    private static readonly Regex TokenPattern = new(@"(?:token|key|secret|password)[=:]\s*\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EmailPattern = new(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Exception is not null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExceptionType", logEvent.Exception.GetType().Name));
        }
    }

    public static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        var sanitized = PhonePattern.Replace(input, "***PHONE***");
        sanitized = TokenPattern.Replace(sanitized, "***REDACTED***");
        sanitized = EmailPattern.Replace(sanitized, "***EMAIL***");

        return sanitized;
    }
}
