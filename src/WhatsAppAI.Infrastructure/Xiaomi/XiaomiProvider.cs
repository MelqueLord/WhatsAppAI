using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Ai;

namespace WhatsAppAI.Infrastructure.Xiaomi;

public sealed class XiaomiProvider(HttpClient httpClient, ILogger<XiaomiProvider> logger) : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt ?? "You are a helpful customer service assistant." }
        };

        foreach (var msg in request.Messages)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        var payload = new
        {
            model = request.ModelId,
            messages,
            max_tokens = request.MaxTokens
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {request.ApiKey}");

        var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError("Xiaomi API error {StatusCode}", (int)httpResponse.StatusCode);
            throw new InvalidOperationException($"Xiaomi API error: {httpResponse.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<XiaomiResponse>(body, JsonOptions);
        if (result is null)
            throw new InvalidOperationException("Failed to parse Xiaomi response");

        var outputText = ExtractOutputText(result);
        var decision = AiDecisionJsonParser.Parse(outputText);

        return new AiResponse
        {
            Decision = decision,
            Content = decision.Action == AiAction.Reply ? decision.Text : null,
            InputTokens = result.Usage?.PromptTokens ?? 0,
            OutputTokens = result.Usage?.CompletionTokens ?? 0,
            RawResponseId = result.Id
        };
    }

    private static string ExtractOutputText(XiaomiResponse response)
    {
        if (response.Choices is null || response.Choices.Count == 0)
            return string.Empty;

        foreach (var choice in response.Choices)
        {
            if (!string.IsNullOrEmpty(choice.Message?.Content))
                return choice.Message.Content;
        }

        return string.Empty;
    }

    private static AiDecision ParseDecision(string output)
    {
        try
        {
            var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            var action = root.TryGetProperty("action", out var actionProp)
                ? actionProp.GetString()?.ToLowerInvariant() switch
                {
                    "reply" => AiAction.Reply,
                    "handoff" => AiAction.Handoff,
                    _ => AiAction.NoAction
                }
                : AiAction.Reply;

            var text = root.TryGetProperty("text", out var textProp) ? textProp.GetString() : output;
            var reason = root.TryGetProperty("handoff_reason", out var reasonProp) ? reasonProp.GetString() : null;
            var confidence = root.TryGetProperty("confidence", out var confProp) ? confProp.GetDouble() : 0.8;
            var queueName = root.TryGetProperty("queue", out var queueProp) ? queueProp.GetString() : null;

            return new AiDecision
            {
                Action = action,
                Text = text,
                HandoffReason = reason,
                Confidence = confidence,
                QueueName = queueName
            };
        }
        catch
        {
            return new AiDecision
            {
                Action = AiAction.Reply,
                Text = output,
                Confidence = 0.5
            };
        }
    }

    private sealed record XiaomiResponse
    {
        public string? Id { get; init; }
        public string? Object { get; init; }
        public List<Choice>? Choices { get; init; }
        public UsageData? Usage { get; init; }
    }

    private sealed record Choice
    {
        public int Index { get; init; }
        public MessageData? Message { get; init; }
        public string? FinishReason { get; init; }
    }

    private sealed record MessageData
    {
        public string? Role { get; init; }
        public string? Content { get; init; }
    }

    private sealed record UsageData
    {
        public int PromptTokens { get; init; }
        public int CompletionTokens { get; init; }
        public int TotalTokens { get; init; }
    }
}
