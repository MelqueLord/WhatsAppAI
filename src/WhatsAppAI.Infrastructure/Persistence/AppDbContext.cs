using Microsoft.EntityFrameworkCore;

namespace WhatsAppAI.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options);
