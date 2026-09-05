using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Automation.Context;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Domain.Knowledge;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Conversations;

public static class AiFeedbackEndpoints
{
    public static IEndpointRouteBuilder MapAiFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations/{conversationId:guid}/messages/{responseMessageId:guid}/ai-feedback")
            .WithTags("AI Feedback")
            .RequireAuthorization("RequireTenantContext");

        group.MapPost("/", SaveAsync).WithName("SaveAiFeedback");
        return app;
    }

    private static async Task<IResult> SaveAsync(
        Guid conversationId,
        Guid responseMessageId,
        [FromBody] SaveAiFeedbackRequest request,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        AppDbContext dbContext,
        IAuditLogRepository auditLogRepository,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (!Enum.TryParse<AiFeedbackRating>(request.Rating, true, out var rating))
            return Results.BadRequest(new { error = "Avaliação inválida. Use Helpful ou NeedsCorrection." });

        var note = request.Note?.Trim();
        var correctedResponse = request.CorrectedResponse?.Trim();
        if (note?.Length > 1000)
            return Results.BadRequest(new { error = "A observação deve ter no máximo 1.000 caracteres." });
        if (correctedResponse?.Length > AiOutputSafetyPolicy.MaxReplyCharacters)
            return Results.BadRequest(new { error = "A resposta corrigida deve ter no máximo 160 caracteres." });
        if (!string.IsNullOrWhiteSpace(correctedResponse) && !AiOutputSafetyPolicy.IsSafe(correctedResponse))
            return Results.BadRequest(new { error = "A resposta corrigida contém dados pessoais ou conteúdo não permitido." });

        var conversation = await conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (currentTenant.UserRole == "Operator")
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(
                currentTenant.UserId.Value, currentTenant.TenantId.Value);
            if (membership is null || !membership.CanAccessQueue(conversation.QueueId))
                return Results.Forbid();
        }

        var interaction = await dbContext.AiInteractions
            .FirstOrDefaultAsync(item => item.TenantId == currentTenant.TenantId.Value &&
                item.ConversationId == conversationId &&
                item.ResponseMessageId == responseMessageId,
                cancellationToken);
        if (interaction is null)
            return Results.NotFound();

        if (rating == AiFeedbackRating.NeedsCorrection &&
            string.IsNullOrWhiteSpace(note) &&
            string.IsNullOrWhiteSpace(correctedResponse))
            return Results.BadRequest(new { error = "Informe a correção ou explique o problema encontrado." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            interaction.RecordFeedback(rating, note, correctedResponse, currentTenant.UserId.Value);

            KnowledgeItem? correctionKnowledge = null;
            if (!string.IsNullOrWhiteSpace(correctedResponse))
            {
                var inboundMessage = await dbContext.Messages
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(item => item.TenantId == currentTenant.TenantId.Value &&
                        item.Id == interaction.MessageId &&
                        item.ConversationId == conversationId,
                        cancellationToken);
                var question = AiContextSanitizer.RedactPersonalData(inboundMessage?.Content);
                var answer = AiContextSanitizer.RedactPersonalData(correctedResponse);
                if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
                {
                    var title = $"Correção do operador: {Limit(question, 170)}";
                    var exists = await dbContext.KnowledgeItems.AnyAsync(item =>
                        item.TenantId == currentTenant.TenantId.Value &&
                        item.IsActive && item.Title == title && item.Content == answer,
                        cancellationToken);
                    if (!exists)
                    {
                        correctionKnowledge = KnowledgeItem.Create(
                            currentTenant.TenantId.Value,
                            title,
                            answer,
                            priority: 25,
                            category: KnowledgeCategories.General);
                        dbContext.KnowledgeItems.Add(correctionKnowledge);
                    }
                }
            }

            dbContext.AiInteractions.Update(interaction);
            dbContext.AuditLogs.Add(AuditLog.Create(
                currentTenant.TenantId.Value,
                currentTenant.UserId,
                "AiResponse.FeedbackRecorded",
                "AiInteraction",
                interaction.Id.ToString(),
                $"rating={rating};correction_knowledge_created={correctionKnowledge is not null}"));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(new
            {
                interactionId = interaction.Id,
                rating = interaction.FeedbackRating.ToString(),
                correctionKnowledgeCreated = correctionKnowledge is not null
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static string Limit(string value, int maxCharacters) =>
        value.Length <= maxCharacters ? value : value[..(maxCharacters - 3)].TrimEnd() + "...";
}

public sealed record SaveAiFeedbackRequest(
    string Rating,
    string? Note,
    string? CorrectedResponse);
