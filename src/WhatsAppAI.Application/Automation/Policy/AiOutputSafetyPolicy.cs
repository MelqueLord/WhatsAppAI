using WhatsAppAI.Application.Automation.Context;

namespace WhatsAppAI.Application.Automation.Policy;

public static class AiOutputSafetyPolicy
{
    public const int MaxReplyCharacters = 300;
    public const string UnsafeContentHandoffReason = "unsafe_content";

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
