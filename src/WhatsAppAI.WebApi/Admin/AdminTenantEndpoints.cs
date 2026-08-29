using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Administration;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Usage;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Admin;

public static class AdminTenantEndpoints
{
    public static IEndpointRouteBuilder MapAdminTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/tenants")
            .WithTags("Admin - Tenants")
            .RequireAuthorization("PlatformAdmin");

        group.MapPost("/", CreateTenantAsync)
            .WithName("CreateTenant");

        group.MapGet("/", GetAllTenantsAsync)
            .WithName("GetAllTenants");

        group.MapGet("/capacity", GetInfrastructureCapacityAsync)
            .WithName("GetInfrastructureCapacity");

        group.MapGet("/{tenantId:guid}", GetTenantByIdAsync)
            .WithName("GetTenantById");

        group.MapGet("/{tenantId:guid}/quota-alerts", GetQuotaAlertsAsync)
            .WithName("GetTenantQuotaAlerts");

        group.MapPut("/{tenantId:guid}", UpdateTenantAsync)
            .WithName("UpdateTenant");

        group.MapPost("/{tenantId:guid}/suspend", SuspendTenantAsync)
            .WithName("SuspendTenant");

        group.MapPost("/{tenantId:guid}/reactivate", ReactivateTenantAsync)
            .WithName("ReactivateTenant");

        group.MapPost("/{tenantId:guid}/payments", RegisterPaymentAsync)
            .WithName("RegisterTenantPayment");

        group.MapPut("/{tenantId:guid}/plan", UpdateTenantPlanAsync)
            .WithName("UpdateTenantPlan");

        group.MapPost("/{tenantId:guid}/owner/reset-password", ResetOwnerPasswordAsync)
            .WithName("ResetTenantOwnerPassword");

        return app;
    }

    private static async Task<IResult> GetInfrastructureCapacityAsync(
        IInfrastructureCapacityReader capacityReader,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var counts = await capacityReader.GetCountsAsync(cancellationToken);
        var customers = InfrastructureCapacityPolicy.Evaluate(
            counts.Customers,
            GetCapacityLimit(configuration, "CustomerLimit", 25));
        var lines = InfrastructureCapacityPolicy.Evaluate(
            counts.Lines,
            GetCapacityLimit(configuration, "LineLimit", 40));
        var operators = InfrastructureCapacityPolicy.Evaluate(
            counts.Operators,
            GetCapacityLimit(configuration, "OperatorLimit", 90));

        return Results.Ok(new InfrastructureCapacityResponse
        {
            Customers = ToResponse(customers),
            Lines = ToResponse(lines),
            Operators = ToResponse(operators),
            MigrationRequired = customers.Status == InfrastructureCapacityStatus.MigrationRequired ||
                lines.Status == InfrastructureCapacityStatus.MigrationRequired ||
                operators.Status == InfrastructureCapacityStatus.MigrationRequired
        });
    }

    private static CapacityIndicatorResponse ToResponse(InfrastructureCapacityIndicator indicator) => new()
    {
        Current = indicator.Current,
        Limit = indicator.Limit,
        UtilizationPercentage = indicator.UtilizationPercentage,
        Status = indicator.Status.ToString()
    };

    private static int GetCapacityLimit(
        IConfiguration configuration,
        string name,
        int defaultValue)
    {
        var configured = configuration.GetValue<int?>($"InfrastructureCapacity:{name}");
        return configured is > 0 ? configured.Value : defaultValue;
    }

    private static async Task<IResult> CreateTenantAsync(
        [FromBody] CreateTenantRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        try
        {
            if (request.OfficialApiLineCount < 0 || request.QrCodeLineCount < 0)
                return Results.BadRequest(new { error = "Line counts cannot be negative." });
            if (request.OperatorLimit < 0)
                return Results.BadRequest(new { error = "Operator limit cannot be negative." });
            if (request.MonthlyAiResponseLimit is < 0)
                return Results.BadRequest(new { error = "Monthly AI response limit cannot be negative." });

            var existingTenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Name == request.Name);
            if (existingTenant is not null)
                return Results.Conflict(new { error = "Tenant with this name already exists." });

            var ownerEmail = request.OwnerEmail.Trim().ToLowerInvariant();
            var existingUser = await dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == ownerEmail);

            if (existingUser is not null)
            {
                var alreadyBelongsToTenant = await dbContext.TenantMemberships
                    .IgnoreQueryFilters()
                    .AnyAsync(m => m.UserId == existingUser.Id);

                if (existingUser.IsPlatformAdmin || alreadyBelongsToTenant)
                    return Results.Conflict(new { error = "User cannot be assigned to another tenant." });
            }

            var slug = TenantSlugHelper.GenerateSlug(request.Name);
            var existingBySlug = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slug);
            if (existingBySlug is not null)
                return Results.Conflict(new { error = "Tenant with this slug already exists." });

            var planCode = request.PlanCode.Trim().ToUpperInvariant();
            var plan = await dbContext.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive);
            if (plan is null || !plan.IsSelectable)
                return Results.BadRequest(new { error = "Invalid or unavailable commercial plan code." });

            var monthlyAiResponseLimit = request.MonthlyAiResponseLimit ??
                plan.DefaultMonthlyAiResponseLimit;

            var tenant = Tenant.Create(
                request.Name,
                slug,
                plan.Id,
                plan.DefaultOfficialApiLineCount,
                0,
                plan.DefaultOperatorLimit,
                monthlyAiResponseLimit);
            tenant.Activate();
            dbContext.Tenants.Add(tenant);

            // Generate temporary password
            var temporaryPassword = GenerateTemporaryPassword();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);

            User owner;
            if (existingUser is not null)
            {
                owner = existingUser;
                owner.Activate(passwordHash);
                owner.SetMustChangePassword(true);
            }
            else
            {
                owner = User.CreateWithTemporaryPassword(ownerEmail, passwordHash, request.OwnerDisplayName);
                dbContext.Users.Add(owner);
            }

            var membership = TenantMembership.Create(tenant.Id, owner, MembershipRole.TenantOwner);
            membership.Activate();
            dbContext.TenantMemberships.Add(membership);

            await dbContext.SaveChangesAsync();

            return Results.Created($"/api/admin/tenants/{tenant.Id}", new CreateTenantResponse
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                Slug = tenant.Slug,
                OwnerEmail = ownerEmail,
                OwnerDisplayName = owner.DisplayName,
                DueDate = tenant.DueDate,
                LastPaymentAt = tenant.LastPaymentAt,
                OfficialApiLineCount = tenant.OfficialApiLineCount,
                QrCodeLineCount = tenant.QrCodeLineCount,
                OperatorLimit = tenant.OperatorLimit,
                MonthlyAiResponseLimit = tenant.MonthlyAiResponseLimit,
                TemporaryPassword = temporaryPassword,
                Message = "Guarde a senha temporária. Ela será exigida no primeiro login e deverá ser alterada."
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to create tenant",
                detail: ex.InnerException?.Message ?? ex.Message,
                statusCode: 500);
        }
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%";

        var password = new char[12];
        password[0] = upper[System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, upper.Length)];
        password[1] = lower[System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, lower.Length)];
        password[2] = digits[System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, digits.Length)];
        password[3] = special[System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, special.Length)];

        const string all = upper + lower + digits + special;
        for (int i = 4; i < password.Length; i++)
        {
            password[i] = all[System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, all.Length)];
        }

        // Shuffle
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    private static async Task<IResult> GetAllTenantsAsync(
        ITenantRepository tenantRepository,
        AppDbContext dbContext,
        IUsageLedgerRepository usageRepository)
    {
        var tenants = await tenantRepository.GetAllAsync();
        var owners = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.Role == MembershipRole.TenantOwner)
            .Select(m => new { m.TenantId, m.User.Email, m.User.DisplayName })
            .ToDictionaryAsync(x => x.TenantId);
        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var aiResponsesByTenant = await usageRepository.GetTotalQuantityByTenantAsync(
            UsageMetricNames.AiResponses, monthStart, monthStart.AddMonths(1));

        return Results.Ok(tenants.Select(t => new TenantResponse
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            PlanId = t.PlanId,
            Status = t.Status.ToString(),
            Version = t.Version,
            CreatedAt = t.CreatedAt,
            DueDate = t.DueDate,
            LastPaymentAt = t.LastPaymentAt,
            OfficialApiLineCount = t.OfficialApiLineCount,
            QrCodeLineCount = t.QrCodeLineCount,
            OperatorLimit = t.OperatorLimit,
            MonthlyAiResponseLimit = t.MonthlyAiResponseLimit,
            MonthlyAiResponsesUsed = aiResponsesByTenant.GetValueOrDefault(t.Id),
            OwnerEmail = owners.TryGetValue(t.Id, out var owner) ? owner.Email : null,
            OwnerDisplayName = owner?.DisplayName,
            SuspendedAt = t.SuspendedAt
        }));
    }

    private static async Task<IResult> GetTenantByIdAsync(
        Guid tenantId,
        ITenantRepository tenantRepository,
        AppDbContext dbContext,
        IUsageLedgerRepository usageRepository)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var aiResponsesUsed = await usageRepository.GetTotalQuantityAsync(
            tenantId,
            UsageMetricNames.AiResponses,
            monthStart,
            monthStart.AddMonths(1));

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version,
            CreatedAt = tenant.CreatedAt,
            DueDate = tenant.DueDate,
            LastPaymentAt = tenant.LastPaymentAt,
            OfficialApiLineCount = tenant.OfficialApiLineCount,
            QrCodeLineCount = tenant.QrCodeLineCount,
            OperatorLimit = tenant.OperatorLimit,
            MonthlyAiResponseLimit = tenant.MonthlyAiResponseLimit,
            MonthlyAiResponsesUsed = aiResponsesUsed,
            SuspendedAt = tenant.SuspendedAt,
            SuspensionReason = tenant.SuspensionReason
        });
    }

    private static async Task<IResult> GetQuotaAlertsAsync(
        Guid tenantId,
        ITenantRepository tenantRepository,
        IAuditLogRepository auditLogRepository)
    {
        if (await tenantRepository.GetByIdAsync(tenantId) is null)
            return Results.NotFound();

        var end = DateTime.UtcNow;
        var alerts = await auditLogRepository.GetByTenantAsync(
            tenantId, end.AddMonths(-3), end, 100);

        return Results.Ok(alerts
            .Where(alert => alert.EntityType == "AiResponseQuota")
            .Select(alert => new
            {
                action = alert.Action,
                entityId = alert.EntityId,
                details = alert.Details,
                occurredAt = alert.OccurredAt
            })
            .ToList());
    }

    private static async Task<IResult> UpdateTenantAsync(
        Guid tenantId,
        [FromBody] UpdateTenantRequest request,
        ITenantRepository tenantRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        if (!TryGetIfMatchVersion(httpContext, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with current version is required." });

        if (tenant.Version != expectedVersion)
            return Results.Conflict(new { error = "Tenant was modified by another request. Please refresh and try again." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Tenant name is required." });
        if (string.IsNullOrWhiteSpace(request.OwnerEmail) || !request.OwnerEmail.Contains('@'))
            return Results.BadRequest(new { error = "Owner email is required." });

        if (request.OfficialApiLineCount < 0 || request.QrCodeLineCount < 0)
            return Results.BadRequest(new { error = "Line counts cannot be negative." });
        if (request.OperatorLimit < 0)
            return Results.BadRequest(new { error = "Operator limit cannot be negative." });
        if (request.MonthlyAiResponseLimit is < 0)
            return Results.BadRequest(new { error = "Monthly AI response limit cannot be negative." });

        var slug = TenantSlugHelper.GenerateSlug(request.Name);
        if (string.IsNullOrWhiteSpace(slug))
            return Results.BadRequest(new { error = "Tenant name must contain letters or numbers." });

        var duplicate = await dbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id != tenantId && (t.Name == request.Name.Trim() || t.Slug == slug));
        if (duplicate)
            return Results.Conflict(new { error = "Tenant with this name or slug already exists." });

        var planCode = request.PlanCode.Trim().ToUpperInvariant();
        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive);
        if (plan is null || (!plan.IsSelectable && plan.Id != tenant.PlanId))
            return Results.BadRequest(new { error = "Invalid or inactive plan code." });

        var owner = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.Role == MembershipRole.TenantOwner)
            .Select(m => m.User)
            .SingleOrDefaultAsync();
        if (owner is null)
            return Results.NotFound(new { error = "Tenant owner not found." });

        var ownerEmail = request.OwnerEmail.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(u => u.Id != owner.Id && u.Email == ownerEmail))
            return Results.Conflict(new { error = "This email is already in use." });

        var officialApiLineCount = plan.IsSelectable
            ? plan.DefaultOfficialApiLineCount
            : request.OfficialApiLineCount;
        var qrCodeLineCount = plan.IsSelectable ? 0 : request.QrCodeLineCount;
        var operatorLimit = plan.IsSelectable
            ? plan.DefaultOperatorLimit
            : request.OperatorLimit;
        var monthlyAiResponseLimit = plan.IsSelectable
            ? request.MonthlyAiResponseLimit ?? plan.DefaultMonthlyAiResponseLimit
            : request.MonthlyAiResponseLimit;

        tenant.UpdateDetails(
            request.Name,
            slug,
            plan.Id,
            officialApiLineCount,
            qrCodeLineCount,
            operatorLimit,
            monthlyAiResponseLimit);
        owner.UpdateEmail(ownerEmail);
        owner.UpdateDisplayName(request.OwnerDisplayName);
        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version,
            CreatedAt = tenant.CreatedAt,
            DueDate = tenant.DueDate,
            LastPaymentAt = tenant.LastPaymentAt,
            OfficialApiLineCount = tenant.OfficialApiLineCount,
            QrCodeLineCount = tenant.QrCodeLineCount,
            OperatorLimit = tenant.OperatorLimit,
            MonthlyAiResponseLimit = tenant.MonthlyAiResponseLimit,
            SuspendedAt = tenant.SuspendedAt,
            SuspensionReason = tenant.SuspensionReason
        });
    }

    private static async Task<IResult> SuspendTenantAsync(
        Guid tenantId,
        [FromBody] SuspendTenantRequest request,
        ITenantRepository tenantRepository,
        HttpContext httpContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        if (!TryGetIfMatchVersion(httpContext, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with current version is required." });

        if (tenant.Version != expectedVersion)
            return Results.Conflict(new { error = "Tenant was modified by another request. Please refresh and try again." });

        try
        {
            tenant.Suspend(request.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version,
            SuspendedAt = tenant.SuspendedAt,
            SuspensionReason = tenant.SuspensionReason
        });
    }

    private static async Task<IResult> RegisterPaymentAsync(
        Guid tenantId,
        [FromBody] RegisterPaymentRequest request,
        ITenantRepository tenantRepository)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        var paidAt = request.PaidAt ?? DateTime.UtcNow;
        if (paidAt > DateTime.UtcNow.AddMinutes(5))
            return Results.BadRequest(new { error = "Payment date cannot be in the future." });

        tenant.RegisterPayment(paidAt);
        await tenantRepository.UpdateAsync(tenant);
        return Results.Ok(new { paidAt = tenant.LastPaymentAt, dueDate = tenant.DueDate, status = tenant.Status.ToString() });
    }

    private static async Task<IResult> ReactivateTenantAsync(
        Guid tenantId,
        ITenantRepository tenantRepository,
        HttpContext httpContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        if (!TryGetIfMatchVersion(httpContext, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with current version is required." });

        if (tenant.Version != expectedVersion)
            return Results.Conflict(new { error = "Tenant was modified by another request. Please refresh and try again." });

        try
        {
            tenant.Reactivate();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version,
            ReactivatedAt = tenant.ReactivatedAt
        });
    }

    private static async Task<IResult> UpdateTenantPlanAsync(
        Guid tenantId,
        [FromBody] UpdatePlanRequest request,
        ITenantRepository tenantRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        if (!TryGetIfMatchVersion(httpContext, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with current version is required." });
        if (tenant.Version != expectedVersion)
            return Results.Conflict(new { error = "Tenant was modified by another request. Please refresh and try again." });

        var planCode = request.PlanCode.Trim().ToUpperInvariant();
        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive);
        if (plan is null || !plan.IsSelectable)
            return Results.BadRequest(new { error = "Invalid or unavailable commercial plan code." });

        if (tenant.PlanId == plan.Id)
            return Results.Ok(new { message = "Tenant already on this plan." });

        tenant.ChangePlan(
            plan.Id,
            plan.DefaultOfficialApiLineCount,
            0,
            plan.DefaultOperatorLimit,
            plan.DefaultMonthlyAiResponseLimit);
        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version,
            MonthlyAiResponseLimit = tenant.MonthlyAiResponseLimit
        });
    }

    private static async Task<IResult> ResetOwnerPasswordAsync(
        Guid tenantId,
        AppDbContext dbContext)
    {
        var owner = await dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.Role == MembershipRole.TenantOwner)
            .Select(m => m.User)
            .SingleOrDefaultAsync();

        if (owner is null)
            return Results.NotFound(new { error = "Tenant owner not found." });

        var temporaryPassword = GenerateTemporaryPassword();
        owner.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(temporaryPassword));
        owner.SetMustChangePassword(true);
        await dbContext.SaveChangesAsync();

        return Results.Ok(new
        {
            email = owner.Email,
            temporaryPassword,
            message = "Senha redefinida. O responsável deverá alterá-la no próximo login."
        });
    }

    private static bool TryGetIfMatchVersion(HttpContext httpContext, out uint version)
    {
        version = 0;
        var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
            return false;

        return uint.TryParse(ifMatch.Trim('"'), out version);
    }
}

public sealed class CreateTenantRequest
{
    public string Name { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
    public string? OwnerDisplayName { get; init; }
    public string PlanCode { get; init; } = "STAR";
    public int OfficialApiLineCount { get; init; }
    public int QrCodeLineCount { get; init; }
    public int OperatorLimit { get; init; }
    public int? MonthlyAiResponseLimit { get; init; }
}

public sealed class CreateTenantResponse
{
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
    public string? OwnerDisplayName { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? LastPaymentAt { get; init; }
    public int OfficialApiLineCount { get; init; }
    public int QrCodeLineCount { get; init; }
    public int OperatorLimit { get; init; }
    public int? MonthlyAiResponseLimit { get; init; }
    public string TemporaryPassword { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class SuspendTenantRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class RegisterPaymentRequest
{
    public DateTime? PaidAt { get; init; }
}

public sealed class UpdatePlanRequest
{
    public string PlanCode { get; init; } = string.Empty;
}

public sealed class UpdateTenantRequest
{
    public string Name { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
    public string? OwnerDisplayName { get; init; }
    public string PlanCode { get; init; } = string.Empty;
    public int OfficialApiLineCount { get; init; }
    public int QrCodeLineCount { get; init; }
    public int OperatorLimit { get; init; }
    public int? MonthlyAiResponseLimit { get; init; }
}

public sealed class TenantResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public Guid PlanId { get; init; }
    public string Status { get; init; } = string.Empty;
    public uint Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? LastPaymentAt { get; init; }
    public int OfficialApiLineCount { get; init; }
    public int QrCodeLineCount { get; init; }
    public int OperatorLimit { get; init; }
    public int? MonthlyAiResponseLimit { get; init; }
    public long MonthlyAiResponsesUsed { get; init; }
    public string? OwnerEmail { get; init; }
    public string? OwnerDisplayName { get; init; }
    public DateTime? SuspendedAt { get; init; }
    public DateTime? ReactivatedAt { get; init; }
    public string? SuspensionReason { get; init; }
}

public sealed class InfrastructureCapacityResponse
{
    public CapacityIndicatorResponse Customers { get; init; } = new();
    public CapacityIndicatorResponse Lines { get; init; } = new();
    public CapacityIndicatorResponse Operators { get; init; } = new();
    public bool MigrationRequired { get; init; }
}

public sealed class CapacityIndicatorResponse
{
    public int Current { get; init; }
    public int Limit { get; init; }
    public int UtilizationPercentage { get; init; }
    public string Status { get; init; } = string.Empty;
}

internal static class TenantSlugHelper
{
    internal static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s-]+", "-");
        return slug.Trim('-');
    }
}
