using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Administration;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Administration;

public sealed class InfrastructureCapacityReader(AppDbContext context)
    : IInfrastructureCapacityReader
{
    public async Task<InfrastructureCapacityCounts> GetCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var customers = await context.Tenants
            .IgnoreQueryFilters()
            .CountAsync(tenant => tenant.Status != TenantStatus.Closed, cancellationToken);

        var lines = await context.WhatsAppAccounts
            .IgnoreQueryFilters()
            .Join(
                context.Tenants.IgnoreQueryFilters()
                    .Where(tenant => tenant.Status != TenantStatus.Closed),
                account => account.TenantId,
                tenant => tenant.Id,
                (account, _) => account)
            .CountAsync(account => account.IsActive, cancellationToken);

        var operators = await context.TenantMemberships
            .IgnoreQueryFilters()
            .Join(
                context.Tenants.IgnoreQueryFilters()
                    .Where(tenant => tenant.Status != TenantStatus.Closed),
                membership => membership.TenantId,
                tenant => tenant.Id,
                (membership, _) => membership)
            .CountAsync(
                membership => membership.Role == MembershipRole.Operator &&
                    membership.Status == MembershipStatus.Active,
                cancellationToken);

        return new InfrastructureCapacityCounts(customers, lines, operators);
    }
}
