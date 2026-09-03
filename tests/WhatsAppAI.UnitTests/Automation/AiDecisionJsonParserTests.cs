using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Ai;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiDecisionJsonParserTests
{
    [Fact]
    public void Parse_ReadsDistinctTags()
    {
        var decision = AiDecisionJsonParser.Parse(
            """{"action":"reply","text":"Olá","confidence":0.9,"tags":["VIP","vip","Novo"]}""");

        Assert.Equal(AiAction.Reply, decision.Action);
        Assert.Equal(new[] { "VIP", "Novo" }, decision.TagNames);
    }

    [Fact]
    public void Parse_ReadsJsonWrappedByModelFormatting()
    {
        var decision = AiDecisionJsonParser.Parse("""
        ```json
        {"action":"reply","text":"Olá","confidence":0.9,"handoff_reason":null,"queue":null,"tags":[]}
        ```
        """);

        Assert.Equal(AiAction.Reply, decision.Action);
        Assert.Equal("Olá", decision.Text);
    }

    [Theory]
    [InlineData("Resposta livre fora do JSON")]
    [InlineData("{\"action\":\"reply\",\"text\":\"Conteúdo sem confiança\"}")]
    [InlineData("{\"action\":\"reply\",\"text\":\"\",\"confidence\":0.9}")]
    [InlineData("{\"action\":\"unknown\",\"text\":\"Conteúdo\",\"confidence\":0.9}")]
    public void Parse_InvalidResponse_ReturnsSafeHandoff(string output)
    {
        var decision = AiDecisionJsonParser.Parse(output);

        Assert.Equal(AiAction.Handoff, decision.Action);
        Assert.Equal("invalid_response", decision.HandoffReason);
        Assert.Null(decision.Text);
    }
}
