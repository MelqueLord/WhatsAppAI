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

builder.Services.AddPersistence(connectionString);

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

builder.Services.AddWorkers();

builder.Services.AddSignalR();
builder.Services.AddSingleton<WhatsAppAI.Application.Abstractions.IRealtimeNotifier,
    WhatsAppAI.WebApi.Hubs.SignalRRealtimeNotifier>();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

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

    await context.Database.MigrateAsync();

    var metaVerifyToken = builder.Configuration["Meta:VerifyToken"];
    var metaAppSecret = builder.Configuration["Meta:AppSecret"];
    if (builder.Environment.IsProduction() &&
        (string.IsNullOrWhiteSpace(metaVerifyToken) || string.IsNullOrWhiteSpace(metaAppSecret)))
    {
        throw new InvalidOperationException("Meta:VerifyToken and Meta:AppSecret are required in production.");
    }

    if (!string.IsNullOrWhiteSpace(metaVerifyToken) && !string.IsNullOrWhiteSpace(metaAppSecret))
    {
        var secretStore = scope.ServiceProvider.GetRequiredService<ISecretStore>();
        await secretStore.SetAsync("meta:verify_token", metaVerifyToken);
        await secretStore.SetAsync("meta:app_secret", metaAppSecret);
    }

    // Optional bootstrap account.
    // Credentials must come from configuration/user-secrets.
    var bootstrapAdminEmail =
        builder.Configuration[
            "BootstrapAdmin:Email"];

    var bootstrapAdminPassword =
        builder.Configuration[
            "BootstrapAdmin:Password"];

    if (
        !string.IsNullOrWhiteSpace(
            bootstrapAdminEmail)
        &&
        !string.IsNullOrWhiteSpace(
            bootstrapAdminPassword))
    {
        try
        {
            var platformAdminExists =
                await context.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        u => u.IsPlatformAdmin);

            if (!platformAdminExists)
            {
                var adminUser =
                    WhatsAppAI.Domain.Identity.User.Create(
                        bootstrapAdminEmail,
                        "Platform Admin");

                adminUser.Activate(
                    BCrypt.Net.BCrypt.HashPassword(
                        bootstrapAdminPassword));

                adminUser.GrantPlatformAdmin();

                context.Users.Add(adminUser);

                await context.SaveChangesAsync();

                Log.Information(
                    "Bootstrap Platform Admin created successfully for {Email}.",
                    bootstrapAdminEmail);
            }
            else
            {
                Log.Information(
                    "A Platform Admin already exists. Bootstrap creation skipped.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Failed to create Bootstrap Platform Admin. Application startup will continue.");
        }
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
            var antiforgery =
                context.RequestServices
                    .GetRequiredService<
                        IAntiforgery>();

            await antiforgery
                .ValidateRequestAsync(
                    context);
        }

        await next();
    });
}

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

app.MapGet(
    "/",
    () =>
        "WhatsApp AI Manager - API Running!");

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
app.MapBroadcastEndpoints();
app.MapPrivacyEndpoints();

app.MapHub<InboxHub>(
    "/hubs/inbox");

await app.RunAsync();
