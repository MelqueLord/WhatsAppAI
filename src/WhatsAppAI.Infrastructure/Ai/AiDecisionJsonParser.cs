using System.Text.Json;
using WhatsAppAI.Application.Automation;

namespace WhatsAppAI.Infrastructure.Ai;

public static class AiDecisionJsonParser
{
    private const string InvalidResponseReason = "invalid_response";

    public static AiDecision Parse(string output)
    {
        try
        {
            var json = ExtractJsonObject(output);
            if (json is null)
                return InvalidDecision();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("action", out var actionValue) ||
                actionValue.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("confidence", out var confidenceValue) ||
                confidenceValue.ValueKind != JsonValueKind.Number ||
                !confidenceValue.TryGetDouble(out var confidence) ||
                confidence is < 0 or > 1)
                return InvalidDecision();

            var action = actionValue.GetString()?.ToLowerInvariant() switch
            {
                "reply" => AiAction.Reply,
                "handoff" => AiAction.Handoff,
                "no_action" or "no_reply" => AiAction.NoAction,
                _ => (AiAction?)null
            };
            if (action is null)
                return InvalidDecision();

            var text = ReadOptionalString(root, "text", out var validText);
            var handoffReason = ReadOptionalString(root, "handoff_reason", out var validReason);
            var queueName = ReadOptionalString(root, "queue", out var validQueue);
            if (!validText || !validReason || !validQueue ||
                action == AiAction.Reply && string.IsNullOrWhiteSpace(text))
                return InvalidDecision();

            var tags = new List<string>();
            if (root.TryGetProperty("tags", out var tagValues))
            {
                if (tagValues.ValueKind != JsonValueKind.Array ||
                    tagValues.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                    return InvalidDecision();

                tags = tagValues.EnumerateArray()
                    .Select(item => item.GetString()?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return new AiDecision
            {
                Action = action.Value,
                Text = text,
                HandoffReason = handoffReason,
                Confidence = confidence,
                QueueName = queueName,
                TagNames = tags
            };
        }
        catch (JsonException)
        {
            return InvalidDecision();
        }
    }

    private static string? ExtractJsonObject(string output)
    {
        var firstBrace = output.IndexOf('{');
        var lastBrace = output.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace
            ? output[firstBrace..(lastBrace + 1)]
            : null;
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName, out bool isValid)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            isValid = true;
            return null;
        }

        isValid = value.ValueKind == JsonValueKind.String;
        return isValid ? value.GetString() : null;
    }

    private static AiDecision InvalidDecision() => new()
    {
        Action = AiAction.Handoff,
        HandoffReason = InvalidResponseReason,
        Confidence = 0
    };
}
