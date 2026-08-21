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
}
