using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Ai;

namespace WhatsAppAI.Infrastructure.Grok;

public sealed class GrokProvider(HttpClient httpClient, ILogger<GrokProvider> logger) : IAiProvider
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
        messages.AddRange(request.Messages.Select(message => new { role = message.Role, content = message.Content }));

        return request.AllowPublicWebSearch
            ? await GetWebGroundedResponseAsync(request, messages, cancellationToken)
            : await GetChatCompletionAsync(request, messages, cancellationToken);
    }

    private async Task<AiResponse> GetChatCompletionAsync(
        AiRequest request,
        List<object> messages,
        CancellationToken cancellationToken)
    {
        var payload = new { model = request.ModelId, messages, max_tokens = request.MaxTokens };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {request.ApiKey}");

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Grok API error {StatusCode}", response.StatusCode);
            throw new InvalidOperationException($"Grok API error ({(int)response.StatusCode} {response.StatusCode})");
        }

        var result = JsonSerializer.Deserialize<GrokResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse Grok response");
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

    private async Task<AiResponse> GetWebGroundedResponseAsync(
        AiRequest request,
        List<object> messages,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = request.ModelId,
            input = messages,
            max_output_tokens = request.MaxTokens,
            tools = new[] { new { type = "web_search" } },
            include = new[] { "no_inline_citations" }
        };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/responses")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {request.ApiKey}");

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Grok Responses API error {StatusCode}", response.StatusCode);
            throw new InvalidOperationException($"Grok API error ({(int)response.StatusCode} {response.StatusCode})");
        }

        var result = JsonSerializer.Deserialize<GrokResponsesResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse Grok Responses API response");
        var output = result.Output
            .SelectMany(item => item.Content)
            .Select(content => content.Text)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? string.Empty;
        var decision = AiDecisionJsonParser.Parse(output);
        return new AiResponse
        {
            Decision = decision,
            Content = decision.Action == AiAction.Reply ? decision.Text : null,
            InputTokens = result.Usage?.InputTokens ?? 0,
            OutputTokens = result.Usage?.OutputTokens ?? 0,
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

    private sealed record GrokResponse
    {
        public string? Id { get; init; }
        public List<Choice>? Choices { get; init; }
        public Usage? Usage { get; init; }
    }

    private sealed record Choice { public Message? Message { get; init; } }
    private sealed record Message { public string? Content { get; init; } }
    private sealed record Usage { public int PromptTokens { get; init; } public int CompletionTokens { get; init; } }
    private sealed record GrokResponsesResponse
    {
        public string? Id { get; init; }
        public List<ResponseOutputItem> Output { get; init; } = [];
        public ResponsesUsage? Usage { get; init; }
    }
    private sealed record ResponseOutputItem { public List<ResponseContentItem> Content { get; init; } = []; }
    private sealed record ResponseContentItem { public string? Text { get; init; } }
    private sealed record ResponsesUsage { public int InputTokens { get; init; } public int OutputTokens { get; init; } }
}
