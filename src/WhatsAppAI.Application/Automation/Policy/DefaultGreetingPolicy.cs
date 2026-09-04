using System.Globalization;
using System.Text;
using WhatsAppAI.Application.Automation;

namespace WhatsAppAI.Application.Automation.Policy;

public static class DefaultGreetingPolicy
{
    private static readonly IReadOnlySet<string> Greetings = new HashSet<string>(StringComparer.Ordinal)
    {
        "oi",
        "ola",
        "bom dia",
        "boa tarde",
        "boa noite",
        "hello",
        "hi",
        "oi tudo bem",
        "ola tudo bem",
        "bom dia tudo bem",
        "boa tarde tudo bem",
        "boa noite tudo bem"
    };

    public static bool IsGreeting(string? content) => Greetings.Contains(Normalize(content));

    public static AiDecision Apply(AiDecision decision, string? content)
    {
        if (!IsGreeting(content) ||
            decision.Action != AiAction.Handoff ||
            decision.HandoffReason is not ("out_of_scope" or "customer_request"))
            return decision;

        return new AiDecision
        {
            Action = AiAction.Reply,
            Text = "Olá! Como posso ajudar?",
            Confidence = Math.Max(decision.Confidence, 0.8),
            TagNames = decision.TagNames
        };
    }

    private static string Normalize(string? content)
    {
        var value = content?.Trim().ToLowerInvariant() ?? string.Empty;
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var withoutAccents = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        var lettersAndSpaces = new string(withoutAccents
            .Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
            .ToArray());
        return string.Join(' ', lettersAndSpaces.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
