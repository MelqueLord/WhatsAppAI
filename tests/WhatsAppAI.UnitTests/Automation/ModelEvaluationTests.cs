using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class ModelEvaluationTests
{
    [Fact]
    public void ApprovalStoresRollbackModel()
    {
        var evaluation = ModelEvaluation.Create(
            Guid.NewGuid(), "gpt-4o-mini", "owner", 0.9, 0.1, 0.95, 0.2m, 500);

        evaluation.Approve("gpt-4o");

        Assert.True(evaluation.IsApproved);
        Assert.Equal("gpt-4o", evaluation.RollbackModelId);
    }

    [Fact]
    public void RejectionClearsApproval()
    {
        var evaluation = ModelEvaluation.Create(
            Guid.NewGuid(), "gpt-4o-mini", "owner", 0.9, 0.1, 0.95, 0.2m, 500);
        evaluation.Approve("gpt-4o");

        evaluation.Reject("safety regression");

        Assert.False(evaluation.IsApproved);
        Assert.Equal("safety regression", evaluation.RejectionReason);
    }
}
