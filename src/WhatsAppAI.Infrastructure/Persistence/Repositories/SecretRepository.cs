using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class SecretRepository(AppDbContext context) : ISecretRepository
{
    public async Task<Secret?> GetByKeyAsync(string key, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        return await context.Set<Secret>()
            .FirstOrDefaultAsync(s => s.Key == key && s.TenantId == tenantId, cancellationToken);
    }

    public async Task AddAsync(Secret secret, CancellationToken cancellationToken = default)
    {
        await context.Set<Secret>().AddAsync(secret, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Secret secret, CancellationToken cancellationToken = default)
    {
        context.Set<Secret>().Update(secret);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string key, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var secret = await GetByKeyAsync(key, tenantId, cancellationToken);
        if (secret is not null)
        {
            context.Set<Secret>().Remove(secret);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
