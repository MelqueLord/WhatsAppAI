using System.Globalization;
using System.Text;

namespace WhatsAppAI.Application.Automation.Policy;

public static class AiConsentOptInPolicy
{
    public const string DefaultPurposeName = "Atendimento automatizado por IA";
    public const string DefaultPurposeDescription =
        "Processamento de mensagens para atendimento automatizado por inteligência artificial.";
    public const string RequestMessage =
        "Para continuar com atendimento automatizado por IA, responda SIM.";
    public const string ConfirmationMessage =
        "Consentimento registrado. Como posso ajudar?";

    public static bool IsAccepted(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var normalized = content.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return string.Equals(builder.ToString(), "SIM", StringComparison.OrdinalIgnoreCase);
    }
}
