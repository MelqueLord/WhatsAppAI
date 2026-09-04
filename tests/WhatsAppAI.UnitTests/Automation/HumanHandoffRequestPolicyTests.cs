using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class HumanHandoffRequestPolicyTests
{
    [Theory]
    [InlineData("Quero falar com um atendente", true)]
    [InlineData("Preciso de um operador", true)]
    [InlineData("Preciso de suporte", false)]
    [InlineData("Aceita cartão?", false)]
    public void IsExplicitHumanRequest_RecognizesOnlyARequestForHumanHelp(
        string message,
        bool expected)
    {
        Assert.Equal(expected, HumanHandoffRequestPolicy.IsExplicitHumanRequest(message));
    }

    [Fact]
    public void KeepConversationAutomatic_ReplacesUnrequestedHandoffWithSafeReply()
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "out_of_scope",
                Confidence = 0.35
            },
            InputTokens = 10,
            OutputTokens = 5
        };

        var result = HumanHandoffRequestPolicy.KeepConversationAutomatic(response);

        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Equal(HumanHandoffRequestPolicy.SafeFallbackReply, result.Content);
        Assert.True(result.Decision.Confidence >= 0.8);
    }

    [Theory]
    [InlineData("out_of_scope", "Aceita cartão?", true)]
    [InlineData("customer_request", "Preciso de suporte", true)]
    [InlineData("customer_request", "Quero falar com atendente", false)]
    [InlineData("sensitive_topic", "Tenho uma emergência", false)]
    public void ShouldKeepConversationAutomatic_PreservesCriticalSafetyHandoffs(
        string handoffReason,
        string message,
        bool expected)
    {
        var decision = new AiDecision
        {
            Action = AiAction.Handoff,
            HandoffReason = handoffReason,
            Confidence = 0.5
        };

        Assert.Equal(expected, HumanHandoffRequestPolicy.ShouldKeepConversationAutomatic(decision, message));
    }

    [Fact]
    public void IsHumanQueueName_RecognizesTheDefaultHumanQueue()
    {
        Assert.True(HumanHandoffRequestPolicy.IsHumanQueueName("ATENDIMENTO HUMANO"));
        Assert.False(HumanHandoffRequestPolicy.IsHumanQueueName("SUPORTE SISTEMA"));
    }
}
