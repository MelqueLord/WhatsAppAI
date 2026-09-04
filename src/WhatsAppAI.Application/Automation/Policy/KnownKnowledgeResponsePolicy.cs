using System.Text.RegularExpressions;

namespace WhatsAppAI.Application.Automation.Policy;

public static class KnownKnowledgeResponsePolicy
{
    public static bool ShouldRequestInference(
        AiResponse response,
        IReadOnlyList<string> relevantKnowledge) =>
        response.Decision.Action == AiAction.Handoff &&
        string.Equals(response.Decision.HandoffReason, "out_of_scope", StringComparison.OrdinalIgnoreCase) &&
        relevantKnowledge.Count > 0;

    public static string BuildInferenceInstruction() =>
        "Reavalie a pergunta usando os fatos autorizados já fornecidos no contexto. A resposta pode ser inferida pela combinação de fatos compatíveis, mesmo que a pergunta use outras palavras. Responda diretamente em action reply quando houver suporte suficiente; não invente, não use exemplos como fatos e só mantenha out_of_scope se a conclusão exigir informação ausente.";

    public static AiResponse RecoverKnownAnswer(
        AiResponse response,
        IReadOnlyList<string> relevantKnowledge)
    {
        if (!ShouldRequestInference(response, relevantKnowledge))
        {
            return response;
        }

        var answer = BuildAnswer(relevantKnowledge);

        if (!AiOutputSafetyPolicy.IsSafe(answer))
            return response;

        var decision = response.Decision with
        {
            Action = AiAction.Reply,
            Text = answer,
            HandoffReason = null,
            Confidence = Math.Max(response.Decision.Confidence, 0.8)
        };
        return response with
        {
            Decision = decision,
            Content = answer
        };
    }

    private static string BuildAnswer(IReadOnlyList<string> relevantKnowledge)
    {
        var planSummaries = relevantKnowledge
            .Select(item =>
            {
                var separator = item.IndexOf(':');
                var title = separator > 0 ? item[..separator].Trim() : item.Trim();
                var price = Regex.Match(item, @"R\$\s*[0-9.]+(?:,[0-9]{1,2})?", RegexOptions.IgnoreCase).Value;
                return title.StartsWith("Plano", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(price)
                    ? $"{title}: {price}"
                    : null;
            })
            .Where(summary => summary is not null)
            .Cast<string>()
            .ToList();

        var answer = planSummaries.Count >= 2
            ? $"Temos {string.Join(", ", planSummaries)}. Qual plano deseja conhecer?"
            : string.Join(" ", relevantKnowledge).Replace("\n", " ", StringComparison.Ordinal).Trim();

        return AiOutputSafetyPolicy.LimitReply(answer);
    }
}
