using System.Text;

namespace WhatsAppAI.Application.Automation.Policy;

public static class CustomerServicePersonalizationPolicy
{
    public static string Build(CustomerServiceContext context)
    {
        var parts = new List<string>
        {
            context.IsReturning
                ? "Este atendimento já possui histórico. Continue do ponto atual, sem repetir boas-vindas nem perguntas já respondidas."
                : "Este é o primeiro contato desta conversa. Apresente-se conforme a identidade da empresa e acolha o pedido atual."
        };

        var displayName = NormalizeDisplayName(context.DisplayName);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            parts.Add($"O nome informado pelo contato é {displayName}. Use apenas o primeiro nome, com naturalidade e no máximo uma vez na resposta; nunca presuma gênero ou outros dados pessoais.");
        }

        if (!string.IsNullOrWhiteSpace(context.QueueName))
        {
            var queueName = Limit(AiContextSanitizer.RedactPersonalData(context.QueueName), 60);
            parts.Add($"A conversa está atualmente na fila {queueName}, ainda em modo automático. Preserve essa fila enquanto responde com as informações autorizadas; estar em fila não é motivo para interromper a IA.");
        }

        parts.Add("Use as mensagens recentes para identificar assunto, etapa e última pergunta pendente. Responda primeiro ao pedido atual, não repita conteúdo já entregue e faça no máximo uma pergunta de continuidade realmente útil.");
        return $"Contexto personalizado deste atendimento: {string.Join(' ', parts)}";
    }

    internal static string? NormalizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Normalize(NormalizationForm.FormC).Trim();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsLetter(character) || character is ' ' or '-' or '\'' or '’')
                builder.Append(character);
        }

        var safeName = string.Join(' ', builder.ToString()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return safeName.Length switch
        {
            0 => null,
            > 60 => safeName[..60].Trim(),
            _ => safeName
        };
    }

    private static string Limit(string? value, int maxCharacters)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= maxCharacters ? text : text[..maxCharacters].Trim();
    }
}
