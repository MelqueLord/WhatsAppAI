using WhatsAppAI.Application.Automation.Context;

namespace WhatsAppAI.Application.Automation.Policy;

public static class CustomerMemoryPolicy
{
    public const int MaxKeyCharacters = 80;
    public const int MaxValueCharacters = 160;

    private static readonly string[] UnsafeMarkers =
    [
        "ignore previous instructions",
        "ignore the previous rules",
        "ignore as instruções anteriores",
        "ignore as instrucoes anteriores",
        "reveal your prompt",
        "revelar seu prompt",
        "system prompt",
        "prompt interno",
        "api key",
        "chave de api",
        "access token",
        "token de acesso",
        "password",
        "senha",
        "secret",
        "segredo"
    ];

    public static bool TryNormalize(
        string? key,
        string? value,
        out string normalizedKey,
        out string normalizedValue,
        out string? error)
    {
        normalizedKey = Normalize(key);
        normalizedValue = Normalize(value);
        error = null;

        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            error = "Informe o nome da memória.";
            return false;
        }

        if (normalizedKey.Length > MaxKeyCharacters)
        {
            error = $"O nome da memória deve ter no máximo {MaxKeyCharacters} caracteres.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            error = "Informe o conteúdo da memória.";
            return false;
        }

        if (normalizedValue.Length > MaxValueCharacters)
        {
            error = $"O conteúdo da memória deve ter no máximo {MaxValueCharacters} caracteres.";
            return false;
        }

        if (AiContextSanitizer.RedactPersonalData(normalizedKey) != normalizedKey ||
            AiContextSanitizer.RedactPersonalData(normalizedValue) != normalizedValue)
        {
            error = "A memória não pode conter telefone, e-mail, CPF ou CNPJ.";
            return false;
        }

        var keyForValidation = normalizedKey;
        var valueForValidation = normalizedValue;
        if (Array.Exists(UnsafeMarkers, marker =>
                keyForValidation.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                valueForValidation.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            error = "A memória contém conteúdo interno ou instruções não permitidas.";
            return false;
        }

        return true;
    }

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
