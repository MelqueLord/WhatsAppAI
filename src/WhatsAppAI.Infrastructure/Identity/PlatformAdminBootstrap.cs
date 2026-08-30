using System.Data;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Identity;

public static class PlatformAdminBootstrap
{
    public static async Task EnsureAsync(
        AppDbContext context,
        IConfiguration configuration,
        Func<string, string> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        ValidateCredentials(email, password);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingAdmin = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.IsPlatformAdmin, cancellationToken);

        if (existingAdmin is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var normalizedEmail = email!.Trim().ToLowerInvariant();
        var existingUser = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (existingUser is not null)
            throw new InvalidOperationException(
                "Bootstrap admin email is already assigned to a non-administrator user.");

        var adminUser = User.CreateWithTemporaryPassword(
            normalizedEmail,
            passwordHasher(password!),
            "Platform Admin");
        adminUser.GrantPlatformAdmin();

        try
        {
            context.Users.Add(adminUser);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (!await context.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.IsPlatformAdmin, cancellationToken))
            {
                throw;
            }
        }
    }

    internal static void ValidateCredentials(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            throw new InvalidOperationException("BootstrapAdmin:Email must be a valid email address.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(c => !char.IsLetterOrDigit(c)))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Password must contain at least 12 characters, including upper/lowercase letters, a number, and a symbol.");
        }
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            var address = new MailAddress(value.Trim());
            return string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
