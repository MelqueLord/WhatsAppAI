using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Integrations;

public static class WhatsAppEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/whatsapp")
            .WithTags("Integrations - WhatsApp")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", GetConfigAsync)
            .WithName("GetWhatsAppConfig");

        group.MapPost("/", SaveConfigAsync)
            .WithName("SaveWhatsAppConfig");

        group.MapPost("/test-connection", TestConnectionAsync)
            .WithName("TestWhatsAppConnection");

        // QR Code connection endpoints
        group.MapGet("/qrcode/{lineNumber:int}", GetQrCodeAsync)
            .WithName("GetWhatsAppQrCode");

        group.MapGet("/session/status/{lineNumber:int}", GetSessionStatusAsync)
            .WithName("GetWhatsAppSessionStatus");

        group.MapPost("/session/disconnect/{lineNumber:int}", DisconnectSessionAsync)
            .WithName("DisconnectWhatsAppSession");

        return app;
    }

    private static async Task<IResult> GetConfigAsync(
        ICurrentTenant currentTenant,
        IWhatsAppAccountRepository accountRepository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var accounts = await accountRepository.GetAllByTenantAsync(currentTenant.TenantId.Value);
        var account = accounts.FirstOrDefault(a => a.ConnectionType == WhatsAppConnectionType.OfficialApi);

        if (account is null)
            return Results.Ok(new WhatsAppConfigResponse
            {
                IsConfigured = false,
                Lines = []
            });

        return Results.Ok(new WhatsAppConfigResponse
        {
            IsConfigured = true,
            WabaId = account.WabaId,
            PhoneNumberId = account.PhoneNumberId,
            IsActive = account.IsActive,
            Lines = accounts.Select(a => new WhatsAppLineResponse
            {
                LineNumber = a.LineNumber,
                ConnectionType = a.ConnectionType.ToString(),
                PhoneNumberId = a.PhoneNumberId,
                IsActive = a.IsActive
            }).ToArray()
        });
    }

    private static async Task<IResult> SaveConfigAsync(
        [FromBody] SaveWhatsAppConfigRequest request,
        ICurrentTenant currentTenant,
        IWhatsAppAccountRepository accountRepository,
        ISecretStore secretStore,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(request.WabaId) ||
            string.IsNullOrWhiteSpace(request.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return Results.BadRequest(new { error = "All fields are required." });
        }

        if (request.LineNumber < 1)
            return Results.BadRequest(new { error = "Line number must be greater than zero." });

        var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
        if (tenant is null || request.LineNumber > tenant.OfficialApiLineCount)
            return Results.BadRequest(new { error = "The selected official API line is outside the contracted quota." });

        var secretKey = $"whatsapp:token:{currentTenant.TenantId}:line:{request.LineNumber}";
        await secretStore.SetAsync(secretKey, request.AccessToken);

        var account = await accountRepository.GetByTenantAndSlotAsync(
            currentTenant.TenantId.Value,
            WhatsAppConnectionType.OfficialApi,
            request.LineNumber);

        if (account is null)
        {
            account = WhatsAppAccount.Create(
                currentTenant.TenantId.Value,
                request.WabaId,
                request.PhoneNumberId,
                secretKey,
                WhatsAppConnectionType.OfficialApi,
                request.LineNumber);

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
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

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

    private static async Task<IResult> GetQrCodeAsync(
        ICurrentTenant currentTenant,
        int lineNumber,
        IWhatsAppClient whatsAppClient,
        IWhatsAppAccountRepository accountRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (lineNumber < 1)
            return Results.BadRequest(new { error = "Line number must be greater than zero." });

        var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
        if (tenant is null || lineNumber > tenant.QrCodeLineCount)
            return Results.BadRequest(new { error = "The selected QR Code line is outside the contracted quota." });

        var account = await accountRepository.GetByTenantAndSlotAsync(
            currentTenant.TenantId.Value,
            WhatsAppConnectionType.QrCode,
            lineNumber);
        if (account is null)
        {
            account = WhatsAppAccount.Create(
                currentTenant.TenantId.Value,
                "whatsapp-web",
                $"qr:{currentTenant.TenantId.Value:D}:{lineNumber}",
                $"whatsapp-web:session:{currentTenant.TenantId.Value:D}:{lineNumber}",
                WhatsAppConnectionType.QrCode,
                lineNumber);
            await accountRepository.AddAsync(account);
        }

        var result = await whatsAppClient.GetQrCodeAsync(currentTenant.TenantId.Value, lineNumber);

        if (!result.IsSuccess)
        {
            if (result.ErrorMessage?.Contains("QR ainda não disponível", StringComparison.OrdinalIgnoreCase) == true)
                return Results.StatusCode(StatusCodes.Status202Accepted);

            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Ok(new
        {
            qrCode = result.QrCodeBase64,
            qrCodeData = result.QrCodeData,
            message = "Scan the QR code with WhatsApp on your phone"
        });
    }

    private static async Task<IResult> GetSessionStatusAsync(
        ICurrentTenant currentTenant,
        int lineNumber,
        IWhatsAppClient whatsAppClient,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (lineNumber < 1)
            return Results.BadRequest(new { error = "Line number must be greater than zero." });

        var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
        if (tenant is null || lineNumber > tenant.QrCodeLineCount)
            return Results.BadRequest(new { error = "The selected QR Code line is outside the contracted quota." });

        var result = await whatsAppClient.GetSessionStatusAsync(currentTenant.TenantId.Value, lineNumber);

        return Results.Ok(new
        {
            isConnected = result.IsConnected,
            phoneNumber = result.PhoneNumber,
            status = result.Status
        });
    }

    private static async Task<IResult> DisconnectSessionAsync(
        ICurrentTenant currentTenant,
        int lineNumber,
        IWhatsAppClient whatsAppClient,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (lineNumber < 1)
            return Results.BadRequest(new { error = "Line number must be greater than zero." });

        var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
        if (tenant is null || lineNumber > tenant.QrCodeLineCount)
            return Results.BadRequest(new { error = "The selected QR Code line is outside the contracted quota." });

        await whatsAppClient.DisconnectSessionAsync(currentTenant.TenantId.Value, lineNumber);

        return Results.Ok(new { message = "Session disconnected successfully." });
    }
}

public sealed class WhatsAppConfigResponse
{
    public bool IsConfigured { get; init; }
    public string? WabaId { get; init; }
    public string? PhoneNumberId { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<WhatsAppLineResponse> Lines { get; init; } = [];
}

public sealed class WhatsAppLineResponse
{
    public int LineNumber { get; init; }
    public string ConnectionType { get; init; } = string.Empty;
    public string PhoneNumberId { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class SaveWhatsAppConfigRequest
{
    public string WabaId { get; init; } = string.Empty;
    public string PhoneNumberId { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public int LineNumber { get; init; } = 1;
}
