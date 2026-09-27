using System.Text.RegularExpressions;

namespace WhatsAppAI.Application.Integrations;

public static partial class WhatsAppTemplateValidator
{
    public static IReadOnlyDictionary<string, string[]> Validate(WhatsAppTemplateCreateRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var name = request.Name?.Trim() ?? string.Empty;
        var language = request.Language?.Trim() ?? string.Empty;
        var body = request.BodyText ?? string.Empty;

        if (name.Length is < 1 or > 512 || !NamePattern().IsMatch(name))
            errors["name"] = ["Use apenas letras minúsculas, números e sublinhados, com até 512 caracteres."];
        if (language.Length is < 2 or > 20 || !LanguagePattern().IsMatch(language))
            errors["language"] = ["Informe um idioma válido, como pt_BR."];
        if (request.Category is not ("UTILITY" or "MARKETING"))
            errors["category"] = ["A categoria deve ser UTILITY ou MARKETING."];
        if (body.Length is < 1 or > 1024)
            errors["bodyText"] = ["O corpo deve ter entre 1 e 1024 caracteres."];
        if (request.FooterText?.Length > 60)
            errors["footerText"] = ["O rodapé deve ter até 60 caracteres."];

        var indexes = PlaceholderPattern().Matches(body)
            .Select(match => int.Parse(match.Groups["index"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .Order()
            .ToArray();
        var malformed = AnyPlaceholderPattern().Matches(body)
            .Select(match => match.Value)
            .Any(value => !PlaceholderPattern().IsMatch(value));
        var expected = Enumerable.Range(1, indexes.Length).ToArray();
        if (malformed || indexes.Length > 10 || !indexes.SequenceEqual(expected))
            errors["bodyText"] = ["As variáveis devem ser posicionais e contínuas, de {{1}} até {{10}}."];
        if (request.BodyExamples.Count != indexes.Length || request.BodyExamples.Any(string.IsNullOrWhiteSpace))
            errors["bodyExamples"] = ["Informe exatamente um exemplo não vazio para cada variável do corpo."];

        return errors;
    }

    [GeneratedRegex("^[a-z0-9_]+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[a-z]{2,3}(?:_[A-Z]{2})?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex LanguagePattern();

    [GeneratedRegex(@"\{\{(?<index>[1-9]\d*)\}\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\{\{[^{}]*\}\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex AnyPlaceholderPattern();
}
