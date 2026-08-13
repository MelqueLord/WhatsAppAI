using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class InvitationRepository(AppDbContext context) : IInvitationRepository
{
    public async Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Invitations
            .Include(i => i.Tenant)
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await context.Invitations
            .Include(i => i.Tenant)
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.Invitations
            .Include(i => i.User)
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> GetPendingByTenantAndEmailAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await context.Invitations
            .Where(i => i.TenantId == tenantId
                && i.Email == normalizedEmail
                && i.Status == InvitationStatus.Pending
                && i.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        await context.Invitations.AddAsync(invitation, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        context.Invitations.Update(invitation);
        await context.SaveChangesAsync(cancellationToken);
    }
}
