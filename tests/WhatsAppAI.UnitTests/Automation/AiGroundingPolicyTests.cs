using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiGroundingPolicyTests
{
    [Fact]
    public void Validate_ConvertsReplyWithUnsupportedPriceToSafeHandoff()
    {
        var response = CreateReply("O Plano Flow custa R$ 299 por mês.");

        var result = AiGroundingPolicy.Validate(
            response,
            ["Plano Flow: R$ 199 por mês."]);

        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("out_of_scope", result.Decision.HandoffReason);
        Assert.Null(result.Decision.Text);
        Assert.Null(result.Content);
    }

    [Fact]
    public void Validate_PreservesReplyWhenConcreteValuesAreAuthorized()
    {
        var response = CreateReply("Atendemos das 8h às 18h. Nosso contato é suporte@empresa.com.");

        var result = AiGroundingPolicy.Validate(
            response,
            ["Horário: atendemos das 8h às 18h.", "Contato: suporte@empresa.com."]);

        Assert.Same(response, result);
    }

    [Fact]
    public void Validate_AllowsGeneralReplyWithoutConcreteValues()
    {
        var response = CreateReply("Sim, posso explicar como funciona o atendimento.");

        var result = AiGroundingPolicy.Validate(response, []);

        Assert.Same(response, result);
    }

    [Fact]
    public void Validate_DoesNotBlockPubliclyGroundedResponse()
    {
        var response = CreateReply("Em geral, essa jornada dura 2 horas.");

        var result = AiGroundingPolicy.Validate(response, [], allowPublicKnowledge: true);

        Assert.Same(response, result);
    }

    [Fact]
    public void Validate_DoesNotChangeHandoffOrNoAction()
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "customer_request",
                Confidence = 0.9
            },
            Content = null
        };

        var result = AiGroundingPolicy.Validate(response, []);

        Assert.Same(response, result);
    }

    private static AiResponse CreateReply(string content) => new()
    {
        Decision = new AiDecision
        {
            Action = AiAction.Reply,
            Text = content,
            Confidence = 0.95
        },
        Content = content
    };
}
