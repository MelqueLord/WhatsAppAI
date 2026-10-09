using System.Globalization;
using System.Text.RegularExpressions;

namespace WhatsAppAI.Application.Integrations;

public static partial class WhatsAppTemplateBodyRenderer
{
    public static string Render(string? bodyText, IReadOnlyList<WhatsAppTemplateParameter> parameters)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
            return string.Empty;

        return PlaceholderRegex().Replace(bodyText, match =>
        {
            var name = match.Groups["name"].Value;
            var namedParameter = parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, name, StringComparison.Ordinal));
            if (namedParameter is not null)
                return namedParameter.Text;

            if (int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var position) &&
                position > 0 && position <= parameters.Count)
            {
                var positionalParameter = parameters[position - 1];
                if (positionalParameter.Name is null || positionalParameter.Name == name)
                    return positionalParameter.Text;
            }

            return match.Value;
        });
    }

    [GeneratedRegex(@"\{\{\s*(?<name>[^{}]+?)\s*\}\}",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex PlaceholderRegex();
}
