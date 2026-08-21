using System.Text.Json;
using WhatsAppAI.Application.Automation;

namespace WhatsAppAI.Infrastructure.Ai;

public static class AiDecisionJsonParser
{
    public static AiDecision Parse(string output)
    {
        try
        {
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            var action = root.TryGetProperty("action", out var actionValue)
                ? actionValue.GetString()?.ToLowerInvariant() switch
                {
                    "reply" => AiAction.Reply,
                    "handoff" => AiAction.Handoff,
                    _ => AiAction.NoAction
                }
                : AiAction.Reply;

            var tags = root.TryGetProperty("tags", out var tagValues) &&
                tagValues.ValueKind == JsonValueKind.Array
                ? tagValues.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : [];

            return new AiDecision
            {
                Action = action,
                Text = root.TryGetProperty("text", out var text) ? text.GetString() : output,
                HandoffReason = root.TryGetProperty("handoff_reason", out var reason) ? reason.GetString() : null,
                Confidence = root.TryGetProperty("confidence", out var confidence) ? confidence.GetDouble() : 0.8,
                QueueName = root.TryGetProperty("queue", out var queue) ? queue.GetString() : null,
                TagNames = tags
            };
        }
        catch (JsonException)
        {
            return new AiDecision { Action = AiAction.Reply, Text = output, Confidence = 0.5 };
        }
    }
}
