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

    private static readonly IReadOnlySet<string> GenericGreetings = new HashSet<string>(StringComparer.Ordinal)
    {
        "ola como posso ajudar",
        "ola como podemos ajudar",
        "oi como posso ajudar",
        "oi como podemos ajudar",
        "ola em que posso ajudar",
        "oi em que posso ajudar"
    };

    public static bool IsGreeting(string? content) => Greetings.Contains(Normalize(content));

    public static bool IsGenericGreeting(string? content) => GenericGreetings.Contains(Normalize(content));

    public static AiDecision Apply(
        AiDecision decision,
        string? content,
        bool isFirstInbound = true,
        string? personalizedWelcome = null)
    {
        if (!isFirstInbound ||
            !IsGreeting(content))
            return decision;

        var greeting = string.IsNullOrWhiteSpace(personalizedWelcome)
            ? "Seja bem-vindo(a)! Como posso ajudar?"
            : Limit(personalizedWelcome, 220);
        if (decision.Action == AiAction.Reply &&
            !string.IsNullOrWhiteSpace(decision.Text) &&
            !IsGenericGreeting(decision.Text))
            return decision;

        return decision with
        {
            Action = AiAction.Reply,
            Text = greeting,
            HandoffReason = null,
            QueueName = null,
            Confidence = Math.Max(decision.Confidence, 0.8),
            TagNames = decision.TagNames,
        };
    }

    private static string Limit(string value, int maxCharacters)
    {
        var text = value.Trim();
        if (text.Length <= maxCharacters)
            return text;
        return maxCharacters <= 3
            ? text[..maxCharacters]
            : $"{text[..(maxCharacters - 3)]}...";
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
