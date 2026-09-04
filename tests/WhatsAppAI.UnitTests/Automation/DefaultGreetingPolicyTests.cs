using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class DefaultGreetingPolicyTests
{
    [Theory]
    [InlineData("oi")]
    [InlineData("Olá!")]
    [InlineData("BOM DIA")]
    [InlineData("boa tarde, tudo bem?")]
    public void IsGreeting_RecognizesShortGreeting(string content)
    {
        Assert.True(DefaultGreetingPolicy.IsGreeting(content));
    }

    [Fact]
    public void Apply_AnswersGreetingWhenModelMisclassifiesItAsHandoff()
    {
        var decision = DefaultGreetingPolicy.Apply(
            new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "out_of_scope",
                Confidence = 0
            },
            "oi");

        Assert.Equal(AiAction.Reply, decision.Action);
        Assert.Equal("Seja bem-vindo(a)! Como posso ajudar?", decision.Text);
        Assert.Null(decision.HandoffReason);
    }

    [Fact]
    public void Apply_ReplacesGenericGreetingWithPersonalizedWelcome()
    {
        var decision = DefaultGreetingPolicy.Apply(
            new AiDecision
            {
                Action = AiAction.Reply,
                Text = "Olá! Como posso ajudar?",
                Confidence = 0.9
            },
            "oi",
            personalizedWelcome: "Seja bem-vindo(a) à Clínica Aurora! Como posso ajudar?");

        Assert.Equal(AiAction.Reply, decision.Action);
        Assert.Equal("Seja bem-vindo(a) à Clínica Aurora! Como posso ajudar?", decision.Text);
    }

    [Fact]
    public void Apply_PreservesNonGenericGreetingGeneratedByTheAgent()
    {
        var decision = new AiDecision
        {
            Action = AiAction.Reply,
            Text = "Seja bem-vindo à Clínica Aurora!",
            Confidence = 0.9
        };

        var result = DefaultGreetingPolicy.Apply(
            decision,
            "oi",
            personalizedWelcome: "Seja bem-vindo(a) à Clínica Aurora! Como posso ajudar?");

        Assert.Same(decision, result);
    }

    [Fact]
    public void Apply_PreservesExplicitHumanRequest()
    {
        var decision = DefaultGreetingPolicy.Apply(
            new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "customer_request",
                Confidence = 0
            },
            "oi preciso falar com um atendente");

        Assert.Equal(AiAction.Handoff, decision.Action);
    }

    [Fact]
    public void Apply_PreservesDecisionForGreetingInAnExistingConversation()
    {
        var decision = DefaultGreetingPolicy.Apply(
            new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "out_of_scope",
                Confidence = 0.7
            },
            "oi",
            isFirstInbound: false);

        Assert.Equal(AiAction.Handoff, decision.Action);
        Assert.Equal("out_of_scope", decision.HandoffReason);
    }
}
