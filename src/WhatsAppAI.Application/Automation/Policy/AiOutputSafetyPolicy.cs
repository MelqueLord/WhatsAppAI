using WhatsAppAI.Application.Automation.Context;

namespace WhatsAppAI.Application.Automation.Policy;

public static class AiOutputSafetyPolicy
{
    public const int MaxReplyCharacters = 160;
    public const string UnsafeContentHandoffReason = "unsafe_content";

    public static string LimitReply(string? content)
    {
        var text = content?.Trim() ?? string.Empty;
        if (text.Length <= MaxReplyCharacters)
            return text;

        var candidate = text[..(MaxReplyCharacters - 3)].TrimEnd();
        var lastSpace = candidate.LastIndexOf(' ');
        if (lastSpace >= MaxReplyCharacters / 2)
            candidate = candidate[..lastSpace].TrimEnd();

        return $"{candidate}...";
    }

    private static readonly string[] SensitiveMarkers =
    [
        "system prompt",
        "system instructions",
        "developer message",
        "internal prompt",
        "prompt interno",
        "instruções internas",
        "instrucoes internas",
        "api key",
        "chave de api",
        "access token",
        "token de acesso",
        "secret",
        "segredo",
        "password",
        "senha"
    ];

    private static readonly string[] MaliciousInstructionMarkers =
    [
        "ignore previous instructions",
        "ignore the previous rules",
        "ignore as instruções anteriores",
        "ignore as instrucoes anteriores",
        "ignore as regras",
        "reveal your prompt",
        "revelar seu prompt",
        "system override",
        "jailbreak"
    ];

    public static bool IsSafe(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return true;

        var normalized = content.Trim();
        if (normalized.Length > MaxReplyCharacters)
            return false;

        if (AiContextSanitizer.RedactPersonalData(normalized) != normalized)
            return false;

        return !SensitiveMarkers.Concat(MaliciousInstructionMarkers)
            .Any(marker => normalized.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
