using System.Globalization;
using System.Text;
using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Application.Automation.Policy;

public static class SemanticKnowledgeMatcher
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "ao", "aos", "as", "com", "como", "da", "das", "de", "do", "dos", "e", "em",
        "essa", "esse", "esta", "este", "eu", "me", "meu", "minha", "na", "nas", "no", "nos",
        "gostaria", "informacao", "o", "os", "para", "por", "preciso", "qual", "que", "quero",
        "saber", "se", "tem", "um", "uma", "voces"
    };

    private static readonly Dictionary<string, string[]> ConceptTerms =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["pricing"] = ["preco", "valor", "custo", "custa", "investimento", "mensal", "mensalidade", "assinatura", "plano", "pacote", "tarifa"],
            ["hours"] = ["horario", "expediente", "atende", "atendem", "funciona", "funcionamento", "aberto", "abre", "fecha", "quando", "dia", "semana", "segunda", "terca", "quarta", "quinta", "sexta", "sabado", "domingo", "disponivel"],
            ["location"] = ["endereco", "local", "localizacao", "onde", "fica", "ficam", "unidade", "loja", "mapa", "chegar", "encontro"],
            ["payment"] = ["pagamento", "pagar", "pix", "cartao", "boleto", "parcela", "parcelamento", "financeiro", "fatura", "cobranca"],
            ["scheduling"] = ["agenda", "agendar", "agendamento", "marcar", "reservar", "reserva", "consulta", "reuniao", "visita", "vaga"],
            ["policy"] = ["politica", "regra", "troca", "devolucao", "cancelamento", "cancelar", "reembolso", "garantia", "prazo"],
            ["delivery"] = ["entrega", "enviar", "envio", "frete", "transportadora", "rastreio", "pedido", "receber"],
            ["support"] = ["suporte", "ajuda", "problema", "erro", "falha", "defeito", "assistencia", "resolver", "conserto"],
            ["overview"] = ["empresa", "negocio", "servico", "produto", "solucao", "sistema", "plataforma", "ferramenta", "automacao", "atendimento", "funcionalidade", "beneficio", "serve", "utilidade"]
        };

    public static int Score(string query, string title, string content, string category)
    {
        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0)
            return 0;

        var titleTerms = Tokenize(title);
        var contentTerms = Tokenize(content);
        var lexicalScore = queryTerms.Sum(term =>
            (titleTerms.Contains(term) ? 4 : 0) +
            (contentTerms.Contains(term) ? 2 : 0));

        var queryConcepts = DetectConcepts(queryTerms);
        var candidateTerms = new HashSet<string>(titleTerms, StringComparer.Ordinal);
        candidateTerms.UnionWith(contentTerms);
        var candidateConcepts = DetectConcepts(candidateTerms);
        var conceptScore = queryConcepts.Intersect(candidateConcepts, StringComparer.Ordinal).Count() * 6;
        var categoryScore = ScoreCategory(category, queryConcepts);
        var fuzzyScore = ScoreFuzzyTerms(queryTerms, candidateTerms);

        var groundedScore = lexicalScore + conceptScore + categoryScore;
        return groundedScore == 0 && fuzzyScore < 2
            ? 0
            : groundedScore + fuzzyScore;
    }

    public static bool IsPricingQuery(string query) =>
        DetectConcepts(Tokenize(query)).Contains("pricing");

    private static HashSet<string> DetectConcepts(HashSet<string> terms)
    {
        var concepts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in ConceptTerms)
        {
            foreach (var term in group.Value)
            {
                if (!terms.Contains(term))
                    continue;

                concepts.Add(group.Key);
                break;
            }
        }

        return concepts;
    }

    private static int ScoreCategory(string category, HashSet<string> queryConcepts)
    {
        if (category.Equals(KnowledgeCategories.Pricing, StringComparison.OrdinalIgnoreCase) && queryConcepts.Contains("pricing"))
            return 8;
        if (category.Equals(KnowledgeCategories.BusinessHours, StringComparison.OrdinalIgnoreCase) && queryConcepts.Contains("hours"))
            return 8;
        if (category.Equals(KnowledgeCategories.Location, StringComparison.OrdinalIgnoreCase) && queryConcepts.Contains("location"))
            return 8;
        if (category.Equals(KnowledgeCategories.Payment, StringComparison.OrdinalIgnoreCase) && queryConcepts.Contains("payment"))
            return 8;
        if (category.Equals(KnowledgeCategories.Policy, StringComparison.OrdinalIgnoreCase) && queryConcepts.Contains("policy"))
            return 8;

        var serviceIntent = queryConcepts.Overlaps(["scheduling", "delivery", "support", "overview"]);
        return serviceIntent && category is KnowledgeCategories.Service or KnowledgeCategories.Faq or KnowledgeCategories.General
            ? 5
            : 0;
    }

    private static int ScoreFuzzyTerms(HashSet<string> queryTerms, HashSet<string> candidateTerms)
    {
        var matches = queryTerms
            .Where(term => term.Length >= 5)
            .Count(queryTerm => candidateTerms.Any(candidateTerm =>
                candidateTerm.Length >= 5 &&
                queryTerm[0] == candidateTerm[0] &&
                DiceSimilarity(queryTerm, candidateTerm) >= 0.78));
        return Math.Min(matches, 2);
    }

    private static double DiceSimilarity(string left, string right)
    {
        if (left.Equals(right, StringComparison.Ordinal))
            return 1;
        if (left.Length < 2 || right.Length < 2)
            return 0;

        var leftPairs = Enumerable.Range(0, left.Length - 1)
            .Select(index => left.Substring(index, 2))
            .ToList();
        var rightPairs = Enumerable.Range(0, right.Length - 1)
            .Select(index => right.Substring(index, 2))
            .ToList();
        var intersection = 0;
        foreach (var pair in leftPairs)
        {
            var match = rightPairs.FindIndex(candidate => candidate.Equals(pair, StringComparison.Ordinal));
            if (match < 0)
                continue;
            intersection++;
            rightPairs.RemoveAt(match);
        }

        return 2d * intersection / (leftPairs.Count + right.Length - 1);
    }

    private static HashSet<string> Tokenize(string value)
    {
        return value
            .Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => new string(token.Where(char.IsLetterOrDigit).ToArray()))
            .Select(NormalizeToken)
            .Where(token => token.Length >= 3 && !StopWords.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string NormalizeToken(string token)
    {
        var decomposed = token.Normalize(NormalizationForm.FormD);
        var normalized = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        return normalized switch
        {
            "custar" or "custou" => "custa",
            "mensais" => "mensal",
            "atendemos" => "atende",
            "funcionamos" => "funciona",
            "enderecos" => "endereco",
            "pagamentos" => "pagamento",
            "agendamentos" => "agendamento",
            "servicos" => "servico",
            "produtos" => "produto",
            "planos" => "plano",
            _ when normalized.Length > 5 && normalized.EndsWith('s') => normalized[..^1],
            _ => normalized
        };
    }
}
