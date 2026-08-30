using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace WhatsAppAI.WebApi.Configuration;

public static class MigrationStartupPolicy
{
    public static bool ShouldApply(IHostEnvironment environment, IConfiguration configuration) =>
        configuration.GetValue<bool?>("Persistence:ApplyMigrationsOnStartup")
        ?? !environment.IsProduction();
}
