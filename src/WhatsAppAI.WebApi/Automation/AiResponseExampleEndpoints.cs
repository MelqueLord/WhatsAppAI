using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Automation;

public static class AiResponseExampleEndpoints
{
    public static IEndpointRouteBuilder MapAiResponseExampleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai-response-examples")
            .WithTags("AI Response Examples")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync);
        group.MapPost("/{id:guid}/reactivate", ReactivateAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(ICurrentTenant currentTenant, IAiResponseExampleRepository repository)
    {
        if (!TryGetTenantOwner(currentTenant, out var tenantId, out var error))
            return error!;

        var examples = await repository.GetByTenantAsync(tenantId);
        return Results.Ok(examples.Select(ToResponse));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] SaveAiResponseExampleRequest request,
        ICurrentTenant currentTenant,
        IAiResponseExampleRepository repository)
    {
        if (!TryGetTenantOwner(currentTenant, out var tenantId, out var error))
            return error!;

        try
        {
            var example = AiResponseExample.Create(tenantId, request.CustomerMessage, request.IdealResponse);
            await repository.AddAsync(example);
            return Results.Created($"/api/ai-response-examples/{example.Id}", ToResponse(example));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] SaveAiResponseExampleRequest request,
        ICurrentTenant currentTenant,
        IAiResponseExampleRepository repository,
        HttpContext httpContext)
    {
        if (!TryGetTenantOwner(currentTenant, out var tenantId, out var error))
            return error!;

        var example = await repository.GetByIdAsync(id);
        if (example is null || example.TenantId != tenantId)
            return Results.NotFound();
        if (!TryGetVersion(httpContext, out var expectedVersion, out error))
            return error!;

        try
        {
            example.Update(request.CustomerMessage, request.IdealResponse, expectedVersion);
            await repository.UpdateAsync(example);
            return Results.Ok(ToResponse(example));
        }
        catch (ConcurrencyException)
        {
            return Results.Conflict(new { error = "O exemplo foi alterado por outro usuário." });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static Task<IResult> DeactivateAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IAiResponseExampleRepository repository,
        HttpContext httpContext) =>
        ChangeStatusAsync(id, false, currentTenant, repository, httpContext);

    private static Task<IResult> ReactivateAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IAiResponseExampleRepository repository,
        HttpContext httpContext) =>
        ChangeStatusAsync(id, true, currentTenant, repository, httpContext);

    private static async Task<IResult> ChangeStatusAsync(
        Guid id,
        bool activate,
        ICurrentTenant currentTenant,
        IAiResponseExampleRepository repository,
        HttpContext httpContext)
    {
        if (!TryGetTenantOwner(currentTenant, out var tenantId, out var error))
            return error!;

        var example = await repository.GetByIdAsync(id);
        if (example is null || example.TenantId != tenantId)
            return Results.NotFound();
        if (!TryGetVersion(httpContext, out var expectedVersion, out error))
            return error!;

        try
        {
            if (activate)
                example.Reactivate(expectedVersion);
            else
                example.Deactivate(expectedVersion);
            await repository.UpdateAsync(example);
            return Results.Ok(ToResponse(example));
        }
        catch (ConcurrencyException)
        {
            return Results.Conflict(new { error = "O exemplo foi alterado por outro usuário." });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static bool TryGetTenantOwner(ICurrentTenant currentTenant, out Guid tenantId, out IResult? error)
    {
        tenantId = currentTenant.TenantId ?? Guid.Empty;
        error = null;
        if (currentTenant.TenantId is null)
        {
            error = Results.Unauthorized();
            return false;
        }
        if (currentTenant.UserRole != "TenantOwner")
        {
            error = Results.Forbid();
            return false;
        }
        return true;
    }

    private static bool TryGetVersion(HttpContext httpContext, out uint version, out IResult? error)
    {
        error = null;
        if (uint.TryParse(httpContext.Request.Headers["If-Match"].FirstOrDefault(), out version))
            return true;
        error = Results.BadRequest(new { error = "If-Match com a versão é obrigatório." });
        return false;
    }

    private static object ToResponse(AiResponseExample example) => new
    {
        id = example.Id,
        customerMessage = example.CustomerMessage,
        idealResponse = example.IdealResponse,
        isActive = example.IsActive,
        version = example.Version,
        createdAt = example.CreatedAt,
        updatedAt = example.UpdatedAt
    };
}

public sealed record SaveAiResponseExampleRequest(string CustomerMessage, string IdealResponse);
