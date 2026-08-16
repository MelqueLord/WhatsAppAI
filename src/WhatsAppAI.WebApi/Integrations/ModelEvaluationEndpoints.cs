using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Integrations;

public static class ModelEvaluationEndpoints
{
    public static IEndpointRouteBuilder MapModelEvaluationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/ai/evaluations")
            .WithTags("Model Evaluations")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListEvaluationsAsync)
            .WithName("ListModelEvaluations");

        group.MapPost("/", CreateEvaluationAsync)
            .WithName("CreateModelEvaluation");

        group.MapPost("/{evaluationId:guid}/approve", ApproveEvaluationAsync)
            .WithName("ApproveModelEvaluation");

        group.MapPost("/{evaluationId:guid}/reject", RejectEvaluationAsync)
            .WithName("RejectModelEvaluation");

        return app;
    }

    private static async Task<IResult> ListEvaluationsAsync(
        ICurrentTenant currentTenant,
        IModelEvaluationRepository evaluationRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "Model evaluations require IA+BOT plan." });

        var evaluations = await evaluationRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        return Results.Ok(evaluations.Select(e => new
        {
            id = e.Id,
            modelId = e.ModelId,
            qualityScore = e.QualityScore,
            handoffRate = e.HandoffRate,
            safetyScore = e.SafetyScore,
            costPer1kTokens = e.CostPer1kTokens,
            p95LatencyMs = e.P95LatencyMs,
            isApproved = e.IsApproved,
            rejectionReason = e.RejectionReason,
            rollbackModelId = e.RollbackModelId,
            evaluatorUserId = e.EvaluatorUserId,
            createdAt = e.CreatedAt
        }));
    }

    private static async Task<IResult> CreateEvaluationAsync(
        [FromBody] CreateEvaluationRequest request,
        ICurrentTenant currentTenant,
        IModelEvaluationRepository evaluationRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "Model evaluations require IA+BOT plan." });

        var evaluation = ModelEvaluation.Create(
            currentTenant.TenantId.Value,
            request.ModelId,
            currentTenant.UserId.ToString()!,
            request.QualityScore,
            request.HandoffRate,
            request.SafetyScore,
            request.CostPer1kTokens,
            request.P95LatencyMs);

        await evaluationRepository.AddAsync(evaluation);

        return Results.Ok(new { id = evaluation.Id });
    }

    private static async Task<IResult> ApproveEvaluationAsync(
        Guid evaluationId,
        [FromBody] ApproveEvaluationRequest? request,
        ICurrentTenant currentTenant,
        IModelEvaluationRepository evaluationRepository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var evaluations = await evaluationRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        var evaluation = evaluations.FirstOrDefault(e => e.Id == evaluationId);
        if (evaluation is null)
            return Results.NotFound();

        evaluation.Approve(request?.RollbackModelId);
        return Results.Ok(new { approved = true });
    }

    private static async Task<IResult> RejectEvaluationAsync(
        Guid evaluationId,
        [FromBody] RejectEvaluationRequest request,
        ICurrentTenant currentTenant,
        IModelEvaluationRepository evaluationRepository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var evaluations = await evaluationRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        var evaluation = evaluations.FirstOrDefault(e => e.Id == evaluationId);
        if (evaluation is null)
            return Results.NotFound();

        evaluation.Reject(request.Reason ?? "Not specified");
        return Results.Ok(new { rejected = true });
    }
}

public sealed record CreateEvaluationRequest
{
    public string ModelId { get; init; } = string.Empty;
    public double QualityScore { get; init; }
    public double HandoffRate { get; init; }
    public double SafetyScore { get; init; }
    public decimal CostPer1kTokens { get; init; }
    public int P95LatencyMs { get; init; }
}

public sealed record ApproveEvaluationRequest
{
    public string? RollbackModelId { get; init; }
}

public sealed record RejectEvaluationRequest
{
    public string? Reason { get; init; }
}
