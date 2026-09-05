using System.Globalization;
using System.Text;

namespace WhatsAppAI.Application.Automation.Policy;

public static class PublicKnowledgePolicy
{
    private static readonly string[] RestrictedCompanyFacts =
    [
        "preco", "valor", "plano", "mensalidade", "desconto", "prazo", "horario",
        "aberto", "endereco", "localizacao", "disponivel", "estoque", "agenda",
        "agendamento", "reserva", "pedido", "entrega", "pagamento", "troca",
        "reembolso", "garantia", "politica", "meu cadastro", "minha conta"
    ];

    private static readonly string[] SensitiveTopics =
    [
        "diagnostico", "medicamento", "sintoma", "tratamento medico", "processo judicial",
        "orientacao juridica", "investimento", "emprestimo", "divida", "emergencia"
    ];

    private static readonly string[] GenericQuestionSignals =
    [
        "o que e", "o que significa", "para que serve", "como funciona", "qual a diferenca",
        "pode explicar", "me explique", "como posso", "quais as vantagens", "quais os beneficios",
        "dicas sobre", "melhor forma", "em geral", "normalmente", "serve para", "e uma solucao",
        "pode ser usado", "pode ser uma", "tem relacao", "funciona com", "ajuda a"
    ];

    public static bool CanUsePublicKnowledge(
        string? customerMessage,
        IReadOnlyList<string> relevantCompanyKnowledge)
    {
        if (string.IsNullOrWhiteSpace(customerMessage) || relevantCompanyKnowledge.Count > 0)
            return false;

        var normalized = Normalize(customerMessage);
        if (RestrictedCompanyFacts.Any(normalized.Contains) || SensitiveTopics.Any(normalized.Contains))
            return false;

        return GenericQuestionSignals.Any(normalized.Contains);
    }

    public static string BuildInstruction() =>
        "Conhecimento público permitido: esta é uma pergunta genérica sem fato correspondente da empresa. Você pode usar conhecimento público geral e, quando o provedor disponibilizar, pesquisa web atual. Diferencie claramente conhecimento geral de fatos da empresa. Não atribua à empresa preço, prazo, serviço, disponibilidade, política ou promessa que não esteja no contexto autorizado. Não inclua links, citações ou Markdown na resposta ao cliente.";

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var withoutAccents = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return string.Join(' ', withoutAccents
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
