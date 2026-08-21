using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class AiProviderCredentialRepository(AppDbContext context) : IAiProviderCredentialRepository
{
    public async Task<AiProviderCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<AiProviderCredential>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<AiProviderCredential?> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.Set<AiProviderCredential>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsActive, cancellationToken);
    }

    public async Task<AiProviderCredential?> GetByTenantAndProviderAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default)
    {
        return await context.Set<AiProviderCredential>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Provider == provider, cancellationToken);
    }

    public async Task AddAsync(AiProviderCredential credential, CancellationToken cancellationToken = default)
    {
        context.Set<AiProviderCredential>().Add(credential);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AiProviderCredential credential, CancellationToken cancellationToken = default)
    {
        context.Set<AiProviderCredential>().Update(credential);
        await context.SaveChangesAsync(cancellationToken);
    }
}
