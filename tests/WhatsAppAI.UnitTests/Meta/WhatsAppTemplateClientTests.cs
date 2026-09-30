using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Infrastructure.Meta;

namespace WhatsAppAI.UnitTests.Meta;

public sealed class WhatsAppTemplateClientTests
{
    [Fact]
    public async Task CreateTemplate_UsesWabaEndpointAndPositionalExample()
    {
        var handler = new CreateTemplateHandler();
        using var httpClient = new HttpClient(handler);
        var client = new WhatsAppClient(httpClient, NullLogger<WhatsAppClient>.Instance);

        var result = await client.CreateTemplateAsync("waba-1", "token-1", new WhatsAppTemplateCreateRequest(
            "atualizacao", "pt_BR", "UTILITY", "Olá {{1}}", ["Maria"], "Equipe"));

        Assert.True(result.IsSuccess);
        Assert.Equal("template-1", result.MetaTemplateId);
        Assert.Equal("Bearer token-1", handler.Authorization);
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal("POSITIONAL", document.RootElement.GetProperty("parameter_format").GetString());
        Assert.Equal("Maria", document.RootElement.GetProperty("components")[0].GetProperty("example").GetProperty("body_text")[0][0].GetString());
    }

    [Fact]
    public async Task ListTemplates_ExplainsWhenMetaDeniesTemplateManagementPermission()
    {
        using var httpClient = new HttpClient(new StatusCodeHandler(HttpStatusCode.Forbidden));
        var client = new WhatsAppClient(httpClient, NullLogger<WhatsAppClient>.Instance);

        var result = await client.ListTemplatesAsync("waba-1", "token-1");

        Assert.False(result.IsSuccess);
        Assert.Equal("A credencial não possui permissão para consultar templates. Configure whatsapp_business_management na Meta.", result.ErrorMessage);
    }

    private sealed class CreateTemplateHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"template-1\",\"status\":\"PENDING\",\"category\":\"UTILITY\"}") };
        }
    }

    private sealed class StatusCodeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
