using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Serilog;
using WhatsAppAI.Infrastructure;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Meta;
using WhatsAppAI.Infrastructure.Observability;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Secrets;
using WhatsAppAI.Infrastructure.Workers;
using WhatsAppAI.WebApi.Admin;
using WhatsAppAI.WebApi.Auth;
using WhatsAppAI.WebApi.Auth.Activate;
using WhatsAppAI.WebApi.Bot;
using WhatsAppAI.WebApi.Contacts;
using WhatsAppAI.WebApi.Conversations;
using WhatsAppAI.WebApi.Dashboard;
using WhatsAppAI.WebApi.Hubs;
using WhatsAppAI.WebApi.Integrations;
using WhatsAppAI.WebApi.Knowledge;
using WhatsAppAI.WebApi.Media;
using WhatsAppAI.WebApi.Operators;
using WhatsAppAI.WebApi.Queues;
using WhatsAppAI.WebApi.Tags;
using WhatsAppAI.WebApi.Usage;
using WhatsAppAI.WebApi.WebhookEvents;
using WhatsAppAI.WebApi.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// PostgreSQL is the default provider; connection comes from ConnectionStrings:DefaultConnection.
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "PostgreSQL";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=whatsappai;Username=postgres;Password=postgres";

builder.Services.AddPersistence(connectionString, dbProvider);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddIdentityServices(builder.Environment);
builder.Services.AddSecretServices();
builder.Services.AddMetaServices(builder.Environment);
builder.Services.AddAiProviderServices();
builder.Services.AddWorkers();

builder.Services.AddSignalR();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });

    options.AddFixedWindowLimiter("webhook", limiterOptions =>
    {
        limiterOptions.PermitLimit = 500;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });

    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Apply database schema and seed
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var ensureCreated = builder.Configuration.GetValue<bool>("DatabaseInitialization:EnsureCreated");
    if (ensureCreated)
    {
        if (context.Database.IsNpgsql())
            await EnsurePostgresSchemaCreatedAsync(context);
        else
            await context.Database.EnsureCreatedAsync();
    }
    else if (!app.Environment.IsProduction())
    {
        await context.Database.MigrateAsync();
    }

    // Optional bootstrap account. Credentials must come from configuration/user-secrets.
    var bootstrapAdminEmail = builder.Configuration["BootstrapAdmin:Email"];
    var bootstrapAdminPassword = builder.Configuration["BootstrapAdmin:Password"];
    if (!string.IsNullOrWhiteSpace(bootstrapAdminEmail) &&
        !string.IsNullOrWhiteSpace(bootstrapAdminPassword) &&
        !context.Users.IgnoreQueryFilters().Any(u => u.IsPlatformAdmin))
    {
        var adminUser = WhatsAppAI.Domain.Identity.User.Create(bootstrapAdminEmail, "Platform Admin");
        adminUser.Activate(BCrypt.Net.BCrypt.HashPassword(bootstrapAdminPassword));
        adminUser.GrantPlatformAdmin();
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();
    }
}

// Seed subscription plans
await app.Services.SeedDefaultPlansAsync();

if (builder.Configuration.GetValue<bool>("DatabaseInitialization:Only"))
    return;

app.UseCors();
app.UseRateLimiter();
app.UseObservability();

if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        var isMutatingMethod = HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method);

        var isApiRequest = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
        var isWebhookRequest = context.Request.Path.StartsWithSegments("/api/webhooks", StringComparison.OrdinalIgnoreCase);
        var isLoginRequest = context.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);
        var isAuthenticatedMutation = context.User.Identity?.IsAuthenticated == true;
        var requiresCsrf = isLoginRequest || isAuthenticatedMutation;

        if (isMutatingMethod && isApiRequest && !isWebhookRequest && requiresCsrf)
        {
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            await antiforgery.ValidateRequestAsync(context);
        }

        await next();
    });
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseIdentityServices();

app.MapGet("/", () => "WhatsApp AI Manager - API Running!");
app.MapHealthCheckEndpoints();
app.MapAntiforgeryBootstrap();
app.MapAuthEndpoints();
app.MapActivateEndpoints();
app.MapAdminTenantEndpoints();
app.MapSubscriptionPlanEndpoints();
app.MapSupportSessionEndpoints();
app.MapOperatorEndpoints();
app.MapWhatsAppEndpoints();
app.MapAiProviderEndpoints();
app.MapModelEvaluationEndpoints();
app.MapKnowledgeEndpoints();
app.MapUsageEndpoints();
app.MapConversationEndpoints();
app.MapConversationModeEndpoints();
app.MapContactEndpoints();
app.MapMediaEndpoints();
app.MapWebhookEndpoints();
app.MapWebhookEventEndpoints();
app.MapClientTagEndpoints();
app.MapBotConfigurationEndpoints();
app.MapServiceLineEndpoints();
app.MapDashboardEndpoints();
app.MapHub<InboxHub>("/hubs/inbox");

await app.RunAsync();

static async Task EnsurePostgresSchemaCreatedAsync(AppDbContext context)
{
    var connection = context.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
        await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('whatsappai.subscription_plans') IS NOT NULL";
        var schemaExists = (bool)(await command.ExecuteScalarAsync() ?? false);
        if (!schemaExists)
            await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
    }
    finally
    {
        if (shouldClose)
            await connection.CloseAsync();
    }
}
