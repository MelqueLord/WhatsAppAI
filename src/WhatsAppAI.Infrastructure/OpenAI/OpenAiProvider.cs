using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Ai;

namespace WhatsAppAI.Infrastructure.OpenAI;

public sealed class OpenAiProvider(HttpClient httpClient, ILogger<OpenAiProvider> logger) : IAiProvider
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
            input = messages,
            max_output_tokens = request.MaxTokens
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {request.ApiKey}");

        var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError("OpenAI API error {StatusCode}: {Body}", httpResponse.StatusCode, body);
            throw new InvalidOperationException($"OpenAI API error: {httpResponse.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<OpenAiResponse>(body, JsonOptions);
        if (result is null)
            throw new InvalidOperationException("Failed to parse OpenAI response");

        var outputText = ExtractOutputText(result);
        var decision = AiDecisionJsonParser.Parse(outputText);

        return new AiResponse
        {
            Decision = decision,
            Content = decision.Action == AiAction.Reply ? decision.Text : null,
            InputTokens = result.Usage.InputTokens,
            OutputTokens = result.Usage.OutputTokens,
            RawResponseId = result.Id
        };
    }

    private static string ExtractOutputText(OpenAiResponse response)
    {
        foreach (var item in response.Output)
        {
            foreach (var content in item.Content)
            {
                if (content.Text is not null)
                    return content.Text;
            }
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

    private sealed record OpenAiResponse
    {
        public string Id { get; init; } = null!;
        public List<OutputItem> Output { get; init; } = [];
        public UsageData Usage { get; init; } = new();
    }

    private sealed record OutputItem
    {
        public string? Type { get; init; }
        public List<ContentItem> Content { get; init; } = [];
    }

    private sealed record ContentItem
    {
        public string? Type { get; init; }
        public string? Text { get; init; }
    }

    private sealed record UsageData
    {
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
    }
}
