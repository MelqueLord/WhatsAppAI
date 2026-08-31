using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Threading.RateLimiting;
using Serilog;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Meta;
using WhatsAppAI.Infrastructure.Observability;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Secrets;
using WhatsAppAI.Infrastructure.Workers;
using WhatsAppAI.WebApi.Admin;
using WhatsAppAI.WebApi.Broadcast;
using WhatsAppAI.WebApi.Auth;
using WhatsAppAI.WebApi.Auth.Activate;
using WhatsAppAI.WebApi.Bot;
using WhatsAppAI.WebApi.Configuration;
using WhatsAppAI.WebApi.Contacts;
using WhatsAppAI.WebApi.Conversations;
using WhatsAppAI.WebApi.Dashboard;
using WhatsAppAI.WebApi.Hubs;
using WhatsAppAI.WebApi.Integrations;
using WhatsAppAI.WebApi.Knowledge;
using WhatsAppAI.WebApi.Media;
using WhatsAppAI.WebApi.Operators;
using WhatsAppAI.WebApi.Privacy;
using WhatsAppAI.WebApi.Queues;
using WhatsAppAI.WebApi.Tags;
using WhatsAppAI.WebApi.Usage;
using WhatsAppAI.WebApi.WebhookEvents;
using WhatsAppAI.WebApi.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

var bridgeWebhookSecret = builder.Configuration["WHATSAPP_WEB_WEBHOOK_SECRET"];
if (!string.IsNullOrWhiteSpace(bridgeWebhookSecret))
{
    builder.Configuration.AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["WhatsAppWeb:WebhookSecret"] = bridgeWebhookSecret
        });
}

var forwardedHeadersConfiguration = builder.Configuration.GetSection("ForwardedHeaders");
var forwardedHeadersEnabled = forwardedHeadersConfiguration.GetValue<bool>("Enabled");
var trustAllForwardedHeaders = forwardedHeadersConfiguration.GetValue<bool>("TrustAll");
var trustedProxyAddresses = forwardedHeadersConfiguration.GetSection("KnownProxies").Get<string[]>() ?? [];
var trustedProxyNetworks = forwardedHeadersConfiguration.GetSection("KnownNetworks").Get<string[]>() ?? [];

if (forwardedHeadersEnabled && !trustAllForwardedHeaders &&
    trustedProxyAddresses.Length == 0 && trustedProxyNetworks.Length == 0 &&
    builder.Environment.IsProduction())
{
    throw new InvalidOperationException(
        "ForwardedHeaders requires KnownProxies, KnownNetworks, or TrustAll in production.");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    if (!forwardedHeadersEnabled)
        return;

    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    if (trustAllForwardedHeaders)
        return;

    foreach (var proxyAddress in trustedProxyAddresses)
    {
        if (!IPAddress.TryParse(proxyAddress, out var proxy))
            throw new InvalidOperationException("ForwardedHeaders:KnownProxies contains an invalid IP address.");

        options.KnownProxies.Add(proxy);
    }

    foreach (var proxyNetwork in trustedProxyNetworks)
    {
        if (!System.Net.IPNetwork.TryParse(proxyNetwork, out var network))
            throw new InvalidOperationException("ForwardedHeaders:KnownNetworks contains an invalid CIDR range.");

        options.KnownIPNetworks.Add(network);
    }
});

// PostgreSQL is the default provider; connection comes from ConnectionStrings:DefaultConnection.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required in production.");

    connectionString = "Host=localhost;Port=5432;Database=whatsappai;Username=whatsappai;Password=postgres";
}

var maxPoolSize = builder.Configuration.GetValue<int?>("Persistence:MaxPoolSize");
builder.Services.AddPersistence(connectionString, maxPoolSize);

builder.Services.AddObservability(
    builder.Configuration);

builder.Services.AddIdentityServices(
    builder.Environment,
    builder.Configuration);

builder.Services.AddSecretServices();

builder.Services.AddMetaServices(
    builder.Environment,
    builder.Configuration);

builder.Services.AddAiProviderServices();

builder.Services.AddWorkers(
    builder.Configuration.GetValue("Workers:Enabled", true));

builder.Services.AddSignalR();
builder.Services.AddSingleton<WhatsAppAI.Application.Abstractions.IRealtimeNotifier,
    WhatsAppAI.WebApi.Hubs.SignalRRealtimeNotifier>();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    options.AddFixedWindowLimiter(
        "fixed",
        limiterOptions =>
        {
            limiterOptions.PermitLimit = 100;
            limiterOptions.Window =
                TimeSpan.FromMinutes(1);

            limiterOptions.QueueProcessingOrder =
                QueueProcessingOrder.OldestFirst;

            limiterOptions.QueueLimit = 10;
        });

    options.AddFixedWindowLimiter(
        "webhook",
        limiterOptions =>
        {
            limiterOptions.PermitLimit = 500;
            limiterOptions.Window =
                TimeSpan.FromMinutes(1);
        });

    options.AddFixedWindowLimiter(
        "auth",
        limiterOptions =>
        {
            limiterOptions.PermitLimit = 20;
            limiterOptions.Window =
                TimeSpan.FromMinutes(1);
        });
});

// CORS
var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? [
        "http://localhost:5173",
        "http://localhost:3000"
    ];

Log.Information(
    "CORS AllowedOrigins: {Origins}",
    string.Join(", ", allowedOrigins));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

var applyMigrationsOnStartup = MigrationStartupPolicy.ShouldApply(
    builder.Environment,
    builder.Configuration);

// IMPORTANTE:
// deve executar antes de qualquer middleware que dependa
// de Request.IsHttps, cookies Secure ou antiforgery.
if (forwardedHeadersEnabled)
    app.UseForwardedHeaders();

// Apply database schema and seed
using (var scope = app.Services.CreateScope())
{
    var context =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    if (applyMigrationsOnStartup)
    {
        await context.Database.MigrateAsync();
    }
    else
    {
        Log.Information("Skipping database migrations on application startup; migration job is responsible for schema changes.");
    }

    var metaVerifyToken = builder.Configuration["Meta:VerifyToken"];
    var metaAppSecret = builder.Configuration["Meta:AppSecret"];

    if (!string.IsNullOrWhiteSpace(metaVerifyToken) && !string.IsNullOrWhiteSpace(metaAppSecret))
    {
        var secretStore = scope.ServiceProvider.GetRequiredService<ISecretStore>();
        await secretStore.SetAsync("meta:verify_token", metaVerifyToken);
        await secretStore.SetAsync("meta:app_secret", metaAppSecret);
    }

    // Bootstrap credentials must come from environment variables or user-secrets.
    // Production must never start without a known PlatformAdmin. A database-only
    // initialization run does not start the application and needs no account.
    if (!builder.Configuration.GetValue<bool>("DatabaseInitialization:Only"))
    {
        var bootstrapAdminEmail = builder.Configuration["BootstrapAdmin:Email"];
        var bootstrapAdminPassword = builder.Configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(bootstrapAdminEmail) ||
            string.IsNullOrWhiteSpace(bootstrapAdminPassword))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Email and BootstrapAdmin:Password are required to initialize the PlatformAdmin.");
        }

        await PlatformAdminBootstrap.EnsureAsync(
            context,
            builder.Configuration,
            BCrypt.Net.BCrypt.HashPassword);
        Log.Information("Platform Admin bootstrap verification completed.");
    }
}

// Seed subscription plans
try
{
    await app.Services
        .SeedDefaultPlansAsync();

    Log.Information(
        "Subscription plans seed completed successfully.");
}
catch (Exception ex)
{
    Log.Error(
        ex,
        "Failed to seed subscription plans. Application startup will continue.");
}

if (
    builder.Configuration
        .GetValue<bool>(
            "DatabaseInitialization:Only"))
{
    return;
}

app.UseCors();

app.UseRateLimiter();

app.UseObservability();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "X-Content-Type-Options",
        "nosniff");

    context.Response.Headers.Append(
        "X-Frame-Options",
        "DENY");

    context.Response.Headers.Append(
        "X-XSS-Protection",
        "1; mode=block");

    context.Response.Headers.Append(
        "Referrer-Policy",
        "strict-origin-when-cross-origin");

    context.Response.Headers.Append(
        "Permissions-Policy",
        "camera=(), microphone=(), geolocation=()");

    await next();
});

app.UseIdentityServices();

// Cookie-authenticated API mutations require an antiforgery token after authentication.
if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        var isMutatingMethod =
            HttpMethods.IsPost(
                context.Request.Method)
            ||
            HttpMethods.IsPut(
                context.Request.Method)
            ||
            HttpMethods.IsPatch(
                context.Request.Method)
            ||
            HttpMethods.IsDelete(
                context.Request.Method);

        var isApiRequest =
            context.Request.Path
                .StartsWithSegments(
                    "/api",
                    StringComparison.OrdinalIgnoreCase);

        var isWebhookRequest =
            context.Request.Path
                .StartsWithSegments(
                    "/api/webhooks",
                    StringComparison.OrdinalIgnoreCase);

        var isAuthenticatedMutation =
            context.User.Identity
                ?.IsAuthenticated == true;

        var isBearerAuthenticated =
            context.Request.Headers.Authorization
                .ToString()
                .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

        var requiresCsrf =
            isAuthenticatedMutation && !isBearerAuthenticated;

        if (
            isMutatingMethod
            &&
            isApiRequest
            &&
            !isWebhookRequest
            &&
            requiresCsrf)
        {
            if (!context.Request.IsHttps)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            try
            {
                var antiforgery =
                    context.RequestServices
                        .GetRequiredService<
                            IAntiforgery>();

                await antiforgery
                    .ValidateRequestAsync(
                        context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode =
                    StatusCodes.Status400BadRequest;
                return;
            }
        }

        await next();
    });
}

app.MapGet(
    "/",
    () =>
        "WhatsApp AI Manager - API Running!");

app.MapHealthCheckEndpoints();
app.MapAntiforgeryBootstrap();
app.MapAuthEndpoints();
app.MapActivateEndpoints();
app.MapAdminTenantEndpoints();
app.MapAdminAiProviderEndpoints();
app.MapAdminAiPricingEndpoints();
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
app.MapBroadcastEndpoints();
app.MapPrivacyEndpoints();

app.MapHub<InboxHub>(
    "/hubs/inbox");

await app.RunAsync();
