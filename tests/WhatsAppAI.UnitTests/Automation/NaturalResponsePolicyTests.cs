using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class NaturalResponsePolicyTests
{
    [Fact]
    public void BuildInstructions_PrioritizesContextualConversationalReplies()
    {
        var instructions = NaturalResponsePolicy.Instructions;

        Assert.Contains("comece pela resposta mais útil", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("histórico", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uma única pergunta específica", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("escolhendo entre opções", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não afirme nem insinue ser uma pessoa", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("naturalidade muda a forma", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("160 caracteres", instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuidelineRule_ExposesNaturalConversationAsBehavior()
    {
        Assert.Equal("natural_conversation", NaturalResponsePolicy.RuleCode);
        Assert.NotEmpty(NaturalResponsePolicy.Description);
    }
}
