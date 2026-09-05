using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiSupervisedLearningPolicyTests
{
    [Fact]
    public void HelpfulFeedback_CreatesApprovedTenantExampleFromSentResponse()
    {
        var example = AiSupervisedLearningPolicy.CreateExampleFromFeedback(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AiFeedbackRating.Helpful,
            "  O que vocês fazem? ",
            "Centralizamos o atendimento no WhatsApp.",
            null);

        Assert.NotNull(example);
        Assert.Equal(AiResponseExampleSource.OperatorFeedback, example!.Source);
        Assert.Equal("Centralizamos o atendimento no WhatsApp.", example.IdealResponse);
    }

    [Fact]
    public void CorrectionFeedback_UsesOnlyTheCorrectedAnswer()
    {
        var example = AiSupervisedLearningPolicy.CreateExampleFromFeedback(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AiFeedbackRating.NeedsCorrection,
            "Qual o horário?",
            "Resposta errada.",
            "Atendemos de segunda a sexta.");

        Assert.NotNull(example);
        Assert.Equal("Atendemos de segunda a sexta.", example!.IdealResponse);
    }

    [Fact]
    public void NoteOnlyCorrection_DoesNotCreateTrainingExample()
    {
        var example = AiSupervisedLearningPolicy.CreateExampleFromFeedback(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AiFeedbackRating.NeedsCorrection,
            "Qual o horário?",
            "Resposta errada.",
            null);

        Assert.Null(example);
    }

    [Fact]
    public void PiiInApprovedAnswer_IsRedactedBeforePersistence()
    {
        var example = AiSupervisedLearningPolicy.CreateExampleFromFeedback(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AiFeedbackRating.Helpful,
            "Olá",
            "Fale com a equipe pelo 71996531915.",
            null);

        Assert.NotNull(example);
        Assert.DoesNotContain("71996531915", example!.IdealResponse);
        Assert.Contains("[redacted]", example.IdealResponse);
    }
}
