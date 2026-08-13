using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Integrations;

public static class WhatsAppEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/whatsapp")
            .WithTags("Integrations - WhatsApp")
            .RequireAuthorization();

        group.MapGet("/", GetConfigAsync)
            .WithName("GetWhatsAppConfig");

        group.MapPost("/", SaveConfigAsync)
            .WithName("SaveWhatsAppConfig")
            ;

        group.MapPost("/test-connection", TestConnectionAsync)
            .WithName("TestWhatsAppConnection")
            ;

        return app;
    }

    private static async Task<IResult> GetConfigAsync(
        ICurrentTenant currentTenant,
        IWhatsAppAccountRepository accountRepository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var account = await accountRepository.GetByTenantAsync(currentTenant.TenantId.Value);

        if (account is null)
            return Results.Ok(new WhatsAppConfigResponse
            {
                IsConfigured = false
            });

        return Results.Ok(new WhatsAppConfigResponse
        {
            IsConfigured = true,
            WabaId = account.WabaId,
            PhoneNumberId = account.PhoneNumberId,
            IsActive = account.IsActive
        });
    }

    private static async Task<IResult> SaveConfigAsync(
        [FromBody] SaveWhatsAppConfigRequest request,
        ICurrentTenant currentTenant,
        IWhatsAppAccountRepository accountRepository,
        ISecretStore secretStore)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.WabaId) ||
            string.IsNullOrWhiteSpace(request.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return Results.BadRequest(new { error = "All fields are required." });
        }

        var secretKey = $"whatsapp:token:{currentTenant.TenantId}";
        await secretStore.SetAsync(secretKey, request.AccessToken);

        var account = await accountRepository.GetByTenantAsync(currentTenant.TenantId.Value);

        if (account is null)
        {
            account = WhatsAppAccount.Create(
                currentTenant.TenantId.Value,
                request.WabaId,
                request.PhoneNumberId,
                secretKey);

            await accountRepository.AddAsync(account);
        }
        else
        {
            account.Update(request.WabaId, request.PhoneNumberId, secretKey);
            await accountRepository.UpdateAsync(account);
        }

        return Results.Ok(new WhatsAppConfigResponse
        {
            IsConfigured = true,
            WabaId = account.WabaId,
            PhoneNumberId = account.PhoneNumberId,
            IsActive = account.IsActive
        });
    }

    private static async Task<IResult> TestConnectionAsync(
        ICurrentTenant currentTenant,
        IWhatsAppAccountRepository accountRepository,
        ISecretStore secretStore,
        IWhatsAppClient whatsAppClient)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var account = await accountRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (account is null)
            return Results.BadRequest(new { error = "WhatsApp not configured." });

        var accessToken = await secretStore.GetAsync(account.AccessTokenRef);
        if (accessToken is null)
            return Results.BadRequest(new { error = "Access token not found." });

        var result = await whatsAppClient.TestConnectionAsync(
            account.PhoneNumberId,
            accessToken);

        if (result.IsSuccess)
        {
            return Results.Ok(new
            {
                success = true,
                message = "Connection successful.",
                phoneNumber = result.PhoneNumber,
                qualityRating = result.QualityRating
            });
        }

        return Results.Ok(new
        {
            success = false,
            message = result.ErrorMessage
        });
    }
}

public sealed class WhatsAppConfigResponse
{
    public bool IsConfigured { get; init; }
    public string? WabaId { get; init; }
    public string? PhoneNumberId { get; init; }
    public bool IsActive { get; init; }
}

public sealed class SaveWhatsAppConfigRequest
{
    public string WabaId { get; init; } = string.Empty;
    public string PhoneNumberId { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
}
