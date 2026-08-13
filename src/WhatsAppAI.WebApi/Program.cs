using Serilog;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Meta;
using WhatsAppAI.Infrastructure.Observability;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Secrets;
using WhatsAppAI.Infrastructure.Workers;
using WhatsAppAI.WebApi.Admin;
using WhatsAppAI.WebApi.Auth;
using WhatsAppAI.WebApi.Auth.Activate;
using WhatsAppAI.WebApi.Conversations;
using WhatsAppAI.WebApi.Hubs;
using WhatsAppAI.WebApi.Integrations;
using WhatsAppAI.WebApi.Media;
using WhatsAppAI.WebApi.Operators;
using WhatsAppAI.WebApi.WebhookEvents;
using WhatsAppAI.WebApi.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Use SQLite for development, PostgreSQL for production
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=whatsappai.db";

builder.Services.AddPersistence(connectionString, dbProvider);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddIdentityServices();
builder.Services.AddSecretServices();
builder.Services.AddMetaServices();
builder.Services.AddWorkers();

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PlatformAdmin", policy => policy.RequireClaim("platform_admin", "true"));

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

app.UseCors();
app.UseObservability();
app.UseIdentityServices();

app.MapGet("/", () => "WhatsApp AI Manager - API Running!");
app.MapHealthCheckEndpoints();
app.MapAntiforgeryBootstrap();
app.MapAuthEndpoints();
app.MapActivateEndpoints();
app.MapAdminTenantEndpoints();
app.MapOperatorEndpoints();
app.MapWhatsAppEndpoints();
app.MapConversationEndpoints();
app.MapConversationModeEndpoints();
app.MapMediaEndpoints();
app.MapWebhookEndpoints();
app.MapWebhookEventEndpoints();
app.MapHub<InboxHub>("/hubs/inbox");

app.Run();
