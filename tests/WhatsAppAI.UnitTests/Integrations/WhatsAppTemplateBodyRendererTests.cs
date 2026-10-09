using WhatsAppAI.Application.Integrations;

namespace WhatsAppAI.UnitTests.Integrations;

public sealed class WhatsAppTemplateBodyRendererTests
{
    [Fact]
    public void Render_ReplacesPositionalAndNamedParameters()
    {
        var body = WhatsAppTemplateBodyRenderer.Render(
            "Olá {{1}}, seu pedido {{pedido}} foi atualizado.",
            [new WhatsAppTemplateParameter("Maria"), new WhatsAppTemplateParameter("A-123", "pedido")]);

        Assert.Equal("Olá Maria, seu pedido A-123 foi atualizado.", body);
    }

    [Fact]
    public void Render_LeavesUnmatchedPlaceholdersVisible()
    {
        var body = WhatsAppTemplateBodyRenderer.Render("Olá {{1}}", []);

        Assert.Equal("Olá {{1}}", body);
    }
}
