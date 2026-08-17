using Microsoft.AspNetCore.RateLimiting;
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
using WhatsAppAI.WebApi.Hubs;
using WhatsAppAI.WebApi.Integrations;
using WhatsAppAI.WebApi.Knowledge;
using WhatsAppAI.WebApi.Media;
using WhatsAppAI.WebApi.Operators;
using WhatsAppAI.WebApi.Tags;
using WhatsAppAI.WebApi.Usage;
using WhatsAppAI.WebApi.WebhookEvents;
using WhatsAppAI.WebApi.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Use SQLite for development, MySQL for production
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? (dbProvider == "SQLite" ? "Data Source=whatsappai.db" : "Server=localhost;Port=3306;Database=whatsappai_dev;User=root;Password=root;CharSet=utf8mb4");

builder.Services.AddPersistence(connectionString, dbProvider);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddIdentityServices();
builder.Services.AddSecretServices();
builder.Services.AddMetaServices(builder.Environment);
builder.Services.AddOpenAiServices();
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
    try
    {
        context.Database.EnsureCreated();
    }
    catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
    {
        // Table already exists — schema is present
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
        context.SaveChanges();
    }
}

// Seed subscription plans
await app.Services.SeedDefaultPlansAsync();

app.UseCors();
app.UseRateLimiter();
app.UseObservability();

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
app.MapHub<InboxHub>("/hubs/inbox");

app.Run();
