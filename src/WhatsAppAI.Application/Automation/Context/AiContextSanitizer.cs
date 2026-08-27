using System.Text.RegularExpressions;

namespace WhatsAppAI.Application.Automation.Context;

public static partial class AiContextSanitizer
{
    public static string RedactPersonalData(string? content)
    {
        var sanitized = content ?? string.Empty;
        sanitized = Email().Replace(sanitized, "[redacted]");
        sanitized = Cnpj().Replace(sanitized, "[redacted]");
        sanitized = Cpf().Replace(sanitized, "[redacted]");
        return Phone().Replace(sanitized, "[redacted]");
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex Email();

    [GeneratedRegex(@"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b")]
    private static partial Regex Cnpj();

    [GeneratedRegex(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b")]
    private static partial Regex Cpf();

    [GeneratedRegex(@"(?<!\d)(?:\+?55\s?)?(?:\(?\d{2}\)?\s?)?\d{4,5}[-\s]?\d{4}(?!\d)")]
    private static partial Regex Phone();
}
