using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Ai;

namespace WhatsAppAI.Infrastructure.Groq;

public sealed class GroqProvider(HttpClient httpClient, ILogger<GroqProvider> logger) : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt ?? "You are a helpful customer service assistant." }
        };
        messages.AddRange(request.Messages.Select(message => new { role = message.Role, content = message.Content }));

        var payload = new
        {
            model = request.ModelId,
            messages,
            max_tokens = request.MaxTokens,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "ai_decision",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", @enum = new[] { "reply", "handoff", "no_action" } },
                            text = new { type = new[] { "string", "null" } },
                            confidence = new { type = "number", minimum = 0, maximum = 1 },
                            handoff_reason = new { type = new[] { "string", "null" } },
                            queue = new { type = new[] { "string", "null" } },
                            tags = new { type = "array", items = new { type = "string" } }
                        },
                        required = new[] { "action", "text", "confidence", "handoff_reason", "queue", "tags" },
                        additionalProperties = false
                    }
                }
            }
        };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "openai/v1/chat/completions")
        {
            Content = JsonContent.Create(payload, options: RequestJsonOptions)
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {request.ApiKey}");

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Groq API error {StatusCode}", (int)response.StatusCode);
            throw new InvalidOperationException($"Groq API error ({(int)response.StatusCode} {response.StatusCode})");
        }

        var result = JsonSerializer.Deserialize<GroqResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse Groq response");
        var output = result.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        var decision = AiDecisionJsonParser.Parse(output);
        return new AiResponse
        {
            Decision = decision,
            Content = decision.Action == AiAction.Reply ? decision.Text : null,
            InputTokens = result.Usage?.PromptTokens ?? 0,
            OutputTokens = result.Usage?.CompletionTokens ?? 0,
            RawResponseId = result.Id
        };
    }

    private static AiDecision ParseDecision(string output)
    {
        try
        {
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            var action = root.TryGetProperty("action", out var actionValue) && actionValue.GetString()?.Equals("handoff", StringComparison.OrdinalIgnoreCase) == true
                ? AiAction.Handoff : AiAction.Reply;
            return new AiDecision
            {
                Action = action,
                Text = root.TryGetProperty("text", out var text) ? text.GetString() : output,
                HandoffReason = root.TryGetProperty("handoff_reason", out var reason) ? reason.GetString() : null,
                Confidence = root.TryGetProperty("confidence", out var confidence) ? confidence.GetDouble() : 0.8,
                QueueName = root.TryGetProperty("queue", out var queue) ? queue.GetString() : null
            };
        }
        catch
        {
            return new AiDecision { Action = AiAction.Reply, Text = output, Confidence = 0.5 };
        }
    }

    private sealed record GroqResponse
    {
        public string? Id { get; init; }
        public List<Choice>? Choices { get; init; }
        public Usage? Usage { get; init; }
    }

    private sealed record Choice { public Message? Message { get; init; } }
    private sealed record Message { public string? Content { get; init; } }
    private sealed record Usage { public int PromptTokens { get; init; } public int CompletionTokens { get; init; } }
}
