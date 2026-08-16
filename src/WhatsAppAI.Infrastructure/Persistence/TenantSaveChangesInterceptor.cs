using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Persistence;

public sealed class TenantSaveChangesInterceptor(IServiceScopeFactory scopeFactory) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not AppDbContext context)
            return base.SavingChanges(eventData, result);

        using var scope = scopeFactory.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var tenantId = currentTenant.TenantId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            if (entry.Metadata.ClrType.Name == "Tenant")
                continue;

            var tenantIdProp = entry.Metadata.FindProperty("TenantId");
            if (tenantIdProp is null)
                continue;

            if (entry.State == EntityState.Added)
            {
                if (tenantId.HasValue)
                {
                    var current = entry.Property("TenantId").CurrentValue;
                    if (current is Guid guid && guid == Guid.Empty)
                        entry.Property("TenantId").CurrentValue = tenantId.Value;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property("TenantId").IsModified = false;
            }
        }

        return base.SavingChanges(eventData, result);
    }
}
