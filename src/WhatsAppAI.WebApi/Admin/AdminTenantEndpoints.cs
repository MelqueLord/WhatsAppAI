using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Administration;
using WhatsAppAI.Application.Automation.Policy;
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

        group.MapGet("/{tenantId:guid}/ai-usage", GetAiUsageAsync)
            .WithName("GetTenantAiUsage");

        group.MapPost("/{tenantId:guid}/ai-response-topups", AddAiResponseTopUpAsync)
            .WithName("AddTenantAiResponseTopUp");

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
            if (request.MonthlyAiTokenLimit is < 0 || request.MonthlyAiCostLimitMinorUnits is < 0)
                return Results.BadRequest(new { error = "AI token and cost limits cannot be negative." });

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
            if (!HasValidLineDistribution(
                    plan.DefaultLineCount, request.OfficialApiLineCount, request.QrCodeLineCount))
                return Results.BadRequest(new
                {
                    error = $"Official API and QR Code line counts must add up to the plan total ({plan.DefaultLineCount})."
                });

            var monthlyAiResponseLimit = request.MonthlyAiResponseLimit ??
                plan.DefaultMonthlyAiResponseLimit;

            var tenant = Tenant.Create(
                request.Name,
                slug,
                plan.Id,
                request.OfficialApiLineCount,
                request.QrCodeLineCount,
                plan.DefaultOperatorLimit,
                monthlyAiResponseLimit,
                request.MonthlyAiTokenLimit,
                request.MonthlyAiCostLimitMinorUnits);
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
                MonthlyAiTokenLimit = tenant.MonthlyAiTokenLimit,
                MonthlyAiCostLimitMinorUnits = tenant.MonthlyAiCostLimitMinorUnits,
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
        var aiResponseTopUpsByTenant = await usageRepository.GetTotalQuantityByTenantAsync(
            UsageMetricNames.AiResponseTopUps, monthStart, monthStart.AddMonths(1));
        var tokenUsageByTenant = await GetMonthlyTokenUsageByTenantAsync(
            dbContext, monthStart, monthStart.AddMonths(1));

        return Results.Ok(tenants.Select(t =>
        {
            var tokenUsage = tokenUsageByTenant.GetValueOrDefault(t.Id);
            var topUps = aiResponseTopUpsByTenant.GetValueOrDefault(t.Id);
            var effectiveLimit = AiResponseQuotaPolicy.GetEffectiveMonthlyLimit(
                t.MonthlyAiResponseLimit, topUps);
            var responsesUsed = aiResponsesByTenant.GetValueOrDefault(t.Id);
            return new TenantResponse
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
                MonthlyAiBaseResponseLimit = t.MonthlyAiResponseLimit,
                MonthlyAiResponseTopUps = topUps,
                MonthlyAiResponseLimit = effectiveLimit,
                MonthlyAiResponsesUsed = responsesUsed,
                MonthlyAiTokensUsed = tokenUsage?.TotalTokens ?? 0,
                MonthlyAiEstimatedCostMinorUnits = tokenUsage?.EstimatedCostMinorUnits ?? 0,
                MonthlyAiResponseStatus = AiQuotaAlertPolicy.GetStatus(
                    effectiveLimit, responsesUsed)
                    .ToString().ToLowerInvariant(),
                IsAiSuspendedByQuota = AiQuotaAlertPolicy.GetStatus(
                    effectiveLimit, responsesUsed) == AiQuotaStatus.Exhausted,
                OwnerEmail = owners.TryGetValue(t.Id, out var owner) ? owner.Email : null,
                OwnerDisplayName = owner?.DisplayName,
                SuspendedAt = t.SuspendedAt
            };
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
        var aiResponseTopUps = await usageRepository.GetTotalQuantityAsync(
            tenantId,
            UsageMetricNames.AiResponseTopUps,
            monthStart,
            monthStart.AddMonths(1));
        var effectiveLimit = AiResponseQuotaPolicy.GetEffectiveMonthlyLimit(
            tenant.MonthlyAiResponseLimit, aiResponseTopUps);
        var tokenUsageByTenant = await GetMonthlyTokenUsageByTenantAsync(
            dbContext, monthStart, monthStart.AddMonths(1));
        var tokenUsage = tokenUsageByTenant.GetValueOrDefault(tenantId);

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
            MonthlyAiBaseResponseLimit = tenant.MonthlyAiResponseLimit,
            MonthlyAiResponseTopUps = aiResponseTopUps,
            MonthlyAiResponseLimit = effectiveLimit,
            MonthlyAiResponsesUsed = aiResponsesUsed,
            MonthlyAiTokensUsed = tokenUsage?.TotalTokens ?? 0,
            MonthlyAiEstimatedCostMinorUnits = tokenUsage?.EstimatedCostMinorUnits ?? 0,
            MonthlyAiResponseStatus = AiQuotaAlertPolicy.GetStatus(
                effectiveLimit, aiResponsesUsed).ToString().ToLowerInvariant(),
            IsAiSuspendedByQuota = AiQuotaAlertPolicy.GetStatus(
                effectiveLimit, aiResponsesUsed) == AiQuotaStatus.Exhausted,
            SuspendedAt = tenant.SuspendedAt,
            SuspensionReason = tenant.SuspensionReason
        });
    }

    private static async Task<IResult> AddAiResponseTopUpAsync(
        Guid tenantId,
        ITenantRepository tenantRepository,
        IUsageLedgerRepository usageRepository,
        IAuditLogRepository auditLogRepository,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 120)
            return Results.BadRequest(new { error = "Idempotency-Key is required and must have at most 120 characters." });

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return Results.NotFound();
        if (tenant.Status == TenantStatus.Closed)
            return Results.BadRequest(new { error = "Closed tenants cannot receive AI response top-ups." });
        if (tenant.MonthlyAiResponseLimit is null)
            return Results.BadRequest(new { error = "Unlimited tenants do not require AI response top-ups." });
        if (!await dbContext.HasAiEnabledAsync(tenantId, cancellationToken))
            return Results.BadRequest(new { error = "AI is not available in this tenant plan." });

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var sourceId = $"{monthStart:yyyy-MM}:{idempotencyKey}";
        var added = false;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({tenantId.ToString()}))",
                cancellationToken);
        }

        var alreadyApplied = await dbContext.UsageLedger
            .IgnoreQueryFilters()
            .AnyAsync(entry => entry.TenantId == tenantId &&
                entry.Provider == "platform" &&
                entry.Metric == UsageMetricNames.AiResponseTopUps &&
                entry.SourceId == sourceId,
                cancellationToken);

        if (!alreadyApplied)
        {
            await usageRepository.AddAsync(UsageLedger.Create(
                tenantId,
                "platform",
                UsageMetricNames.AiResponseTopUps,
                sourceId,
                AiResponseQuotaPolicy.TopUpQuantity,
                "responses"), cancellationToken);
            await auditLogRepository.AddAsync(AuditLog.Create(
                tenantId,
                currentTenant.UserId,
                "Tenant.AiQuotaTopUpAdded",
                "AiResponseQuota",
                sourceId,
                $"period={monthStart:yyyy-MM};quantity={AiResponseQuotaPolicy.TopUpQuantity}"),
                cancellationToken);
            added = true;
        }

        var topUps = await usageRepository.GetTotalQuantityAsync(
            tenantId, UsageMetricNames.AiResponseTopUps, monthStart, monthEnd, cancellationToken);
        var used = await usageRepository.GetTotalQuantityAsync(
            tenantId, UsageMetricNames.AiResponses, monthStart, monthEnd, cancellationToken);
        var effectiveLimit = AiResponseQuotaPolicy.GetEffectiveMonthlyLimit(
            tenant.MonthlyAiResponseLimit, topUps);
        var status = AiQuotaAlertPolicy.GetStatus(effectiveLimit, used);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new
        {
            added,
            quantity = added ? AiResponseQuotaPolicy.TopUpQuantity : 0,
            baseLimit = tenant.MonthlyAiResponseLimit,
            topUps,
            limit = effectiveLimit,
            used,
            remaining = effectiveLimit is null ? (long?)null : Math.Max(0, effectiveLimit.Value - used),
            status = status.ToString().ToLowerInvariant(),
            aiSuspended = status == AiQuotaStatus.Exhausted
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

    private static async Task<IResult> GetAiUsageAsync(
        Guid tenantId,
        ITenantRepository tenantRepository,
        AppDbContext dbContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var tokenRows = await dbContext.UsageLedger
            .IgnoreQueryFilters()
            .Where(usage => usage.TenantId == tenantId &&
                usage.RecordedAt >= monthStart && usage.RecordedAt < monthEnd &&
                (usage.Metric == "input_tokens" || usage.Metric == "output_tokens"))
            .GroupBy(usage => new { usage.Provider, usage.Metric })
            .Select(group => new
            {
                provider = group.Key.Provider,
                metric = group.Key.Metric,
                tokens = group.Sum(usage => usage.Quantity),
                estimatedCostMinorUnits = group.Sum(usage => usage.CostMinorUnits ?? 0)
            })
            .ToListAsync();

        var modelRows = await dbContext.AiInteractions
            .IgnoreQueryFilters()
            .Where(interaction => interaction.TenantId == tenantId &&
                interaction.CreatedAt >= monthStart && interaction.CreatedAt < monthEnd)
            .GroupBy(interaction => interaction.ModelId)
            .Select(group => new
            {
                modelId = group.Key,
                inputTokens = group.Sum(interaction => interaction.InputTokens),
                outputTokens = group.Sum(interaction => interaction.OutputTokens),
                interactions = group.Count()
            })
            .OrderByDescending(item => item.inputTokens + item.outputTokens)
            .ToListAsync();

        var responseUsed = await dbContext.UsageLedger
            .IgnoreQueryFilters()
            .Where(usage => usage.TenantId == tenantId &&
                usage.Metric == UsageMetricNames.AiResponses &&
                usage.RecordedAt >= monthStart && usage.RecordedAt < monthEnd)
            .SumAsync(usage => (long?)usage.Quantity) ?? 0;
        var responseTopUps = await dbContext.UsageLedger
            .IgnoreQueryFilters()
            .Where(usage => usage.TenantId == tenantId &&
                usage.Metric == UsageMetricNames.AiResponseTopUps &&
                usage.RecordedAt >= monthStart && usage.RecordedAt < monthEnd)
            .SumAsync(usage => (long?)usage.Quantity) ?? 0;
        var effectiveResponseLimit = AiResponseQuotaPolicy.GetEffectiveMonthlyLimit(
            tenant.MonthlyAiResponseLimit, responseTopUps);

        var activeModel = await dbContext.AiProviderCredentials
            .IgnoreQueryFilters()
            .Where(credential => credential.TenantId == tenantId && credential.IsActive)
            .Select(credential => new { credential.Provider, credential.ModelId })
            .FirstOrDefaultAsync();

        var inputTokens = tokenRows
            .Where(row => row.metric == "input_tokens")
            .Sum(row => row.tokens);
        var outputTokens = tokenRows
            .Where(row => row.metric == "output_tokens")
            .Sum(row => row.tokens);
        var totalTokens = inputTokens + outputTokens;
        var totalCost = tokenRows.Sum(row => row.estimatedCostMinorUnits);

        return Results.Ok(new
        {
            periodStart = monthStart,
            periodEnd = monthEnd,
            contractedModel = activeModel,
            responsePackage = new
            {
                baseLimit = tenant.MonthlyAiResponseLimit,
                topUps = responseTopUps,
                limit = effectiveResponseLimit,
                used = responseUsed,
                remaining = effectiveResponseLimit is null
                    ? (long?)null
                    : Math.Max(0, effectiveResponseLimit.Value - responseUsed),
                status = AiQuotaAlertPolicy.GetStatus(effectiveResponseLimit, responseUsed)
                    .ToString().ToLowerInvariant(),
                aiSuspended = AiQuotaAlertPolicy.GetStatus(effectiveResponseLimit, responseUsed) == AiQuotaStatus.Exhausted
            },
            tokens = new
            {
                input = inputTokens,
                output = outputTokens,
                total = totalTokens,
                estimatedCostMinorUnits = totalCost
            },
            budget = new
            {
                tokenLimit = tenant.MonthlyAiTokenLimit,
                tokenUsed = totalTokens,
                tokenRemaining = tenant.MonthlyAiTokenLimit is null
                    ? (long?)null
                    : Math.Max(0, tenant.MonthlyAiTokenLimit.Value - totalTokens),
                costLimitMinorUnits = tenant.MonthlyAiCostLimitMinorUnits,
                costUsedMinorUnits = totalCost,
                costRemainingMinorUnits = tenant.MonthlyAiCostLimitMinorUnits is null
                    ? (long?)null
                    : Math.Max(0, tenant.MonthlyAiCostLimitMinorUnits.Value - totalCost),
                status = (tenant.MonthlyAiTokenLimit is not null && totalTokens >= tenant.MonthlyAiTokenLimit.Value) ||
                    (tenant.MonthlyAiCostLimitMinorUnits is not null && totalCost >= tenant.MonthlyAiCostLimitMinorUnits.Value)
                    ? "exhausted"
                    : "available"
            },
            byProvider = tokenRows.Select(row => new
            {
                provider = row.provider,
                metric = row.metric,
                tokens = row.tokens,
                estimatedCostMinorUnits = row.estimatedCostMinorUnits
            }),
            byModel = modelRows
        });
    }

    private static async Task<IReadOnlyDictionary<Guid, TenantAiTokenUsage>> GetMonthlyTokenUsageByTenantAsync(
        AppDbContext dbContext,
        DateTime from,
        DateTime toExclusive)
    {
        var rows = await dbContext.UsageLedger
            .IgnoreQueryFilters()
            .Where(usage => usage.RecordedAt >= from && usage.RecordedAt < toExclusive &&
                (usage.Metric == "input_tokens" || usage.Metric == "output_tokens"))
            .GroupBy(usage => usage.TenantId)
            .Select(group => new
            {
                tenantId = group.Key,
                totalTokens = group.Sum(usage => usage.Quantity),
                estimatedCostMinorUnits = group.Sum(usage => usage.CostMinorUnits ?? 0)
            })
            .ToListAsync();

        return rows.ToDictionary(
            row => row.tenantId,
            row => new TenantAiTokenUsage(row.totalTokens, row.estimatedCostMinorUnits));
    }

    private static async Task<IResult> UpdateTenantAsync(
        Guid tenantId,
        [FromBody] UpdateTenantRequest request,
        ITenantRepository tenantRepository,
        AppDbContext dbContext,
        HttpContext httpContext,
        IAuditLogRepository auditLogRepository)
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
        if (request.MonthlyAiTokenLimit is < 0 || request.MonthlyAiCostLimitMinorUnits is < 0)
            return Results.BadRequest(new { error = "AI token and cost limits cannot be negative." });

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
        if (plan.IsSelectable && !HasValidLineDistribution(
                plan.DefaultLineCount, request.OfficialApiLineCount, request.QrCodeLineCount))
            return Results.BadRequest(new
            {
                error = $"Official API and QR Code line counts must add up to the plan total ({plan.DefaultLineCount})."
            });

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

        var officialApiLineCount = request.OfficialApiLineCount;
        var qrCodeLineCount = request.QrCodeLineCount;
        var operatorLimit = plan.IsSelectable
            ? plan.DefaultOperatorLimit
            : request.OperatorLimit;
        var monthlyAiResponseLimit = plan.IsSelectable
            ? request.MonthlyAiResponseLimit ?? plan.DefaultMonthlyAiResponseLimit
            : request.MonthlyAiResponseLimit;
        var previousPlanId = tenant.PlanId;
        var previousMonthlyAiResponseLimit = tenant.MonthlyAiResponseLimit;

        tenant.UpdateDetails(
            request.Name,
            slug,
            plan.Id,
            officialApiLineCount,
            qrCodeLineCount,
            operatorLimit,
            monthlyAiResponseLimit,
            request.MonthlyAiTokenLimit,
            request.MonthlyAiCostLimitMinorUnits);
        owner.UpdateEmail(ownerEmail);
        owner.UpdateDisplayName(request.OwnerDisplayName);
        await tenantRepository.UpdateAsync(tenant);
        if (previousPlanId != tenant.PlanId || previousMonthlyAiResponseLimit != tenant.MonthlyAiResponseLimit)
        {
            await auditLogRepository.AddAsync(AuditLog.Create(
                tenant.Id,
                null,
                "Tenant.AiQuotaChanged",
                "Tenant",
                tenant.Id.ToString(),
                $"version={tenant.Version};previous_limit={previousMonthlyAiResponseLimit?.ToString() ?? "unlimited"};new_limit={tenant.MonthlyAiResponseLimit?.ToString() ?? "unlimited"}"));
        }

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
            MonthlyAiTokenLimit = tenant.MonthlyAiTokenLimit,
            MonthlyAiCostLimitMinorUnits = tenant.MonthlyAiCostLimitMinorUnits,
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
        HttpContext httpContext,
        IAuditLogRepository auditLogRepository)
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

        var currentPlan = await dbContext.SubscriptionPlans.FindAsync(tenant.PlanId);
        var monthlyAiResponseLimit = PlanQuotaPolicy.ResolveMonthlyAiResponseLimit(
            tenant.MonthlyAiResponseLimit,
            currentPlan?.DefaultMonthlyAiResponseLimit,
            plan.DefaultMonthlyAiResponseLimit);

        var officialApiLineCount = request.OfficialApiLineCount ??
            (request.QrCodeLineCount is int requestedQrCodeLineCount
                ? plan.DefaultLineCount - requestedQrCodeLineCount
                : Math.Min(tenant.OfficialApiLineCount, plan.DefaultLineCount));
        var qrCodeLineCount = request.QrCodeLineCount ??
            (plan.DefaultLineCount - officialApiLineCount);
        if (!HasValidLineDistribution(plan.DefaultLineCount, officialApiLineCount, qrCodeLineCount))
            return Results.BadRequest(new
            {
                error = $"Official API and QR Code line counts must add up to the plan total ({plan.DefaultLineCount})."
            });

        tenant.ChangePlan(
            plan.Id,
            officialApiLineCount,
            qrCodeLineCount,
            plan.DefaultOperatorLimit,
            monthlyAiResponseLimit);
        await tenantRepository.UpdateAsync(tenant);
        await auditLogRepository.AddAsync(AuditLog.Create(
            tenant.Id,
            null,
            "Tenant.PlanChanged",
            "Tenant",
            tenant.Id.ToString(),
            $"version={tenant.Version};plan={plan.Code};monthly_limit={monthlyAiResponseLimit?.ToString() ?? "unlimited"}"));

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

    private static bool HasValidLineDistribution(
        int totalLineCount,
        int officialApiLineCount,
        int qrCodeLineCount) =>
        officialApiLineCount >= 0 &&
        qrCodeLineCount >= 0 &&
        (long)officialApiLineCount + qrCodeLineCount == totalLineCount;
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
    public long? MonthlyAiTokenLimit { get; init; }
    public long? MonthlyAiCostLimitMinorUnits { get; init; }
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
    public long? MonthlyAiTokenLimit { get; init; }
    public long? MonthlyAiCostLimitMinorUnits { get; init; }
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
    public int? OfficialApiLineCount { get; init; }
    public int? QrCodeLineCount { get; init; }
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
    public long? MonthlyAiTokenLimit { get; init; }
    public long? MonthlyAiCostLimitMinorUnits { get; init; }
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
    public int? MonthlyAiBaseResponseLimit { get; init; }
    public long MonthlyAiResponseTopUps { get; init; }
    public int? MonthlyAiResponseLimit { get; init; }
    public long? MonthlyAiTokenLimit { get; init; }
    public long? MonthlyAiCostLimitMinorUnits { get; init; }
    public long MonthlyAiResponsesUsed { get; init; }
    public long MonthlyAiTokensUsed { get; init; }
    public long MonthlyAiEstimatedCostMinorUnits { get; init; }
    public string MonthlyAiResponseStatus { get; init; } = "unlimited";
    public bool IsAiSuspendedByQuota { get; init; }
    public string? OwnerEmail { get; init; }
    public string? OwnerDisplayName { get; init; }
    public DateTime? SuspendedAt { get; init; }
    public DateTime? ReactivatedAt { get; init; }
    public string? SuspensionReason { get; init; }
}

public sealed record TenantAiTokenUsage(long TotalTokens, long EstimatedCostMinorUnits);

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
