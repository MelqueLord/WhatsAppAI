using System.Globalization;
using System.Text;

namespace WhatsAppAI.Application.Automation.Policy;

public static class HumanHandoffRequestPolicy
{
    public const string SafeFallbackReply =
        "Não tenho uma informação confirmada sobre isso agora. Posso ajudar com outra dúvida ou chamar um atendente se você preferir.";

    private static readonly string[] HumanRequestTerms =
    [
        "atendente",
        "atendimento humano",
        "falar com humano",
        "falar com uma pessoa",
        "falar com alguem",
        "falar com alguém",
        "operador"
    ];

    public static bool IsExplicitHumanRequest(string? messageContent)
    {
        var normalizedMessage = $" {Normalize(messageContent)} ";
        return HumanRequestTerms.Any(term =>
            normalizedMessage.Contains($" {Normalize(term)}", StringComparison.Ordinal));
    }

    public static bool IsHumanQueueName(string queueName)
    {
        var normalizedName = Normalize(queueName);
        return normalizedName.Contains("atendimento humano", StringComparison.Ordinal) ||
            normalizedName.Equals("humano", StringComparison.Ordinal);
    }

    public static bool ShouldKeepConversationAutomatic(AiDecision decision, string? messageContent)
    {
        return decision.HandoffReason is "out_of_scope" ||
            (decision.HandoffReason is "customer_request" && !IsExplicitHumanRequest(messageContent));
    }

    public static AiResponse KeepConversationAutomatic(AiResponse response)
    {
        return response with
        {
            Decision = new AiDecision
            {
                Action = AiAction.Reply,
                Text = SafeFallbackReply,
                Confidence = Math.Max(response.Decision.Confidence, 0.8),
                TagNames = response.Decision.TagNames
            },
            Content = SafeFallbackReply
        };
    }

    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        var previousWasSpace = true;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToLowerInvariant(character));
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                normalized.Append(' ');
                previousWasSpace = true;
            }
        }

        return normalized.ToString().Trim();
    }
}
