using WhatsAppAI.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is required. Configure it with user-secrets or an environment variable.");

builder.Services.AddPostgreSqlPersistence(connectionString);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
