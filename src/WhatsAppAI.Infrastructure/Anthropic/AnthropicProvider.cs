using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Automation;

namespace WhatsAppAI.Infrastructure.Anthropic;

public sealed class AnthropicProvider(HttpClient httpClient, ILogger<AnthropicProvider> logger) : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var messages = new List<object>();

        foreach (var msg in request.Messages)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        var payload = new
        {
            model = request.ModelId,
            max_tokens = request.MaxTokens,
            system = request.SystemPrompt ?? "You are a helpful customer service assistant.",
            messages
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Add("x-api-key", request.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError("Anthropic API error {StatusCode}: {Body}", httpResponse.StatusCode, body);
            throw new InvalidOperationException($"Anthropic API error: {httpResponse.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<AnthropicResponse>(body, JsonOptions);
        if (result is null)
            throw new InvalidOperationException("Failed to parse Anthropic response");

        var outputText = ExtractOutputText(result);
        var decision = ParseDecision(outputText);

        return new AiResponse
        {
            Decision = decision,
            Content = decision.Action == AiAction.Reply ? decision.Text : null,
            InputTokens = result.Usage?.InputTokens ?? 0,
            OutputTokens = result.Usage?.OutputTokens ?? 0,
            RawResponseId = result.Id
        };
    }

    private static string ExtractOutputText(AnthropicResponse response)
    {
        if (response.Content is null)
            return string.Empty;

        foreach (var block in response.Content)
        {
            if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
                return block.Text;
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

            return new AiDecision
            {
                Action = action,
                Text = text,
                HandoffReason = reason,
                Confidence = confidence
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

    private sealed record AnthropicResponse
    {
        public string? Id { get; init; }
        public string? Type { get; init; }
        public string? Role { get; init; }
        public List<ContentBlock>? Content { get; init; }
        public string? Model { get; init; }
        public string? StopReason { get; init; }
        public UsageData? Usage { get; init; }
    }

    private sealed record ContentBlock
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
