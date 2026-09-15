namespace WhatsAppAI.Application.Automation.Policy;

public static class BusinessServicePlaybookPolicy
{
    public static string? Build(
        string? serviceGoal,
        string? qualificationData,
        string? serviceProcess,
        string? handoffCriteria)
    {
        var parts = new List<string>
        {
            "Antes de responder, identifique internamente a etapa atual entre acolhimento, descoberta, orientação, decisão, suporte e encaminhamento. " +
            "Continue da última etapa concluída no histórico, sem mostrar o nome da etapa, reiniciar o roteiro ou pedir novamente um dado já informado. " +
            "Faça no máximo uma pergunta necessária por mensagem e avance para o próximo passo configurado assim que houver informação suficiente."
        };

        Add(parts, "Objetivo deste atendimento", serviceGoal, 140);
        Add(parts, "Dados necessários para qualificar o pedido", qualificationData, 170);
        Add(parts, "Processo que deve ser seguido", serviceProcess, 210);
        Add(parts, "Situações que exigem encaminhamento", handoffCriteria, 170);

        if (parts.Count == 1)
            return null;

        return string.Join(' ', parts);
    }

    private static void Add(List<string> parts, string label, string? value, int maxCharacters)
    {
        var normalized = value?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var limited = normalized.Length <= maxCharacters
                ? normalized
                : $"{normalized[..(maxCharacters - 3)]}...";
            parts.Add($"{label}: {limited}.");
        }
    }
}
