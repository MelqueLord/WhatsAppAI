using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Automation;

namespace WhatsAppAI.Infrastructure.Gemini;

public sealed class GeminiProvider(HttpClient httpClient, ILogger<GeminiProvider> logger) : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        var contents = new List<object>();

        foreach (var msg in request.Messages)
        {
            contents.Add(new
            {
                role = msg.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = msg.Content } }
            });
        }

        object? systemInstruction = null;
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            systemInstruction = new
            {
                parts = new[] { new { text = request.SystemPrompt } }
            };
        }

        var payload = new
        {
            contents,
            systemInstruction,
            generationConfig = new
            {
                maxOutputTokens = request.MaxTokens
            }
        };

        var modelId = request.ModelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? request.ModelId["models/".Length..]
            : request.ModelId;
        var url = $"v1beta/models/{Uri.EscapeDataString(modelId)}:generateContent?key={Uri.EscapeDataString(request.ApiKey)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var providerMessage = ExtractProviderError(body);
            logger.LogError("Gemini API error {StatusCode}: {ProviderMessage}", httpResponse.StatusCode, providerMessage);
            throw new InvalidOperationException($"Gemini API error ({(int)httpResponse.StatusCode} {httpResponse.StatusCode}): {providerMessage}");
        }

        var result = JsonSerializer.Deserialize<GeminiResponse>(body, JsonOptions);
        if (result is null)
            throw new InvalidOperationException("Failed to parse Gemini response");

        var outputText = ExtractOutputText(result);
        var decision = ParseDecision(outputText);

        return new AiResponse
        {
            Decision = decision,
            Content = decision.Action == AiAction.Reply ? decision.Text : null,
            InputTokens = result.UsageMetadata?.PromptTokenCount ?? 0,
            OutputTokens = result.UsageMetadata?.CandidatesTokenCount ?? 0,
            RawResponseId = null
        };
    }

    private static string ExtractProviderError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var messageProperty)
                    ? messageProperty.GetString()
                    : null;
                if (!string.IsNullOrWhiteSpace(message))
                    return message.Length > 240 ? message[..240] : message;
            }
        }
        catch
        {
            // Keep the provider response out of the user-facing error when it is not JSON.
        }

        return "O Google recusou a solicitação. Verifique a API habilitada, o modelo e a chave.";
    }

    private static string ExtractOutputText(GeminiResponse response)
    {
        if (response.Candidates is null || response.Candidates.Count == 0)
            return string.Empty;

        foreach (var candidate in response.Candidates)
        {
            if (candidate.Content?.Parts is null) continue;
            foreach (var part in candidate.Content.Parts)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    return part.Text;
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

    private sealed record GeminiResponse
    {
        public List<Candidate>? Candidates { get; init; }
        public UsageMetadataData? UsageMetadata { get; init; }
    }

    private sealed record Candidate
    {
        public ContentData? Content { get; init; }
        public string? FinishReason { get; init; }
    }

    private sealed record ContentData
    {
        public List<PartData>? Parts { get; init; }
        public string? Role { get; init; }
    }

    private sealed record PartData
    {
        public string? Text { get; init; }
    }

    private sealed record UsageMetadataData
    {
        public int PromptTokenCount { get; init; }
        public int CandidatesTokenCount { get; init; }
        public int TotalTokenCount { get; init; }
    }
}
