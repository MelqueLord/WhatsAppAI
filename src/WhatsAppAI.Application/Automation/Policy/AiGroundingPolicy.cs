using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WhatsAppAI.Application.Automation.Policy;

public static class AiGroundingPolicy
{
    public const string RuleCode = "grounded_response";
    public const string UnsupportedFactHandoffReason = "out_of_scope";

    public const string Description =
        "Bloqueie valores concretos que não estejam no contexto autorizado antes do envio.";

    private static readonly Regex ConcreteValuePattern = new(
        @"(?:\b(?:r\$|rs\$|us\$|\$)\s*\d+(?:[.,]\d{1,2})?|\b\d{1,2}(?::\d{2})?\s*(?:h|hrs?|horas?|min(?:utos?)?)\b|\b\d+(?:[.,]\d+)?\s*(?:%|reais?|centavos?|dias?|semanas?|mes(?:es)?|anos?|km|quil[oô]metros?)\b|\b\d{1,2}/\d{1,2}(?:/\d{2,4})?\b|\b(?:19|20)\d{2}\b|https?://\S+|www\.\S+|[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}|\+?\d[\d\s().-]{7,}\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string BuildInstructions() =>
        "Segurança factual: antes de responder, confira se cada preço, horário, prazo, percentual, data, link ou contato concreto aparece no contexto autorizado. Se não aparecer, não complete a informação por plausibilidade: use handoff com handoff_reason \"out_of_scope\". Afirmações gerais sem valores concretos podem ser respondidas somente quando forem sustentadas pelo perfil, diretrizes ou conhecimento fornecidos.";

    public static AiResponse Validate(
        AiResponse response,
        IReadOnlyList<string> authorizedContext,
        bool allowPublicKnowledge = false)
    {
        if (allowPublicKnowledge ||
            response.Decision.Action != AiAction.Reply ||
            string.IsNullOrWhiteSpace(response.Content))
        {
            return response;
        }

        var source = Canonicalize(string.Join('\n', authorizedContext));
        if (!ContainsUnsupportedConcreteValue(response.Content, source))
            return response;

        var decision = response.Decision with
        {
            Action = AiAction.Handoff,
            HandoffReason = UnsupportedFactHandoffReason,
            Text = null,
            QueueName = null
        };
        return response with { Decision = decision, Content = null };
    }

    internal static bool ContainsUnsupportedConcreteValue(string content, string canonicalAuthorizedContext)
    {
        foreach (Match match in ConcreteValuePattern.Matches(content))
        {
            var value = Canonicalize(match.Value);
            if (!string.IsNullOrWhiteSpace(value) &&
                !canonicalAuthorizedContext.Contains(value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Canonicalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value
            .Trim()
            .TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
