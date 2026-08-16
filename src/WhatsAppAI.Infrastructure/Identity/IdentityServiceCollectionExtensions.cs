using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Identity;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IAuthorizationHandler, TenantContextHandler>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/api/auth/login";
                options.LogoutPath = "/api/auth/logout";
                options.AccessDeniedPath = "/api/auth/access-denied";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Name = "whatsappai.session";
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireTenantContext", policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new TenantContextRequirement()))
            .AddPolicy("PlatformAdmin", policy =>
                policy.RequireClaim("platform_admin", "true"));

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "whatsappai.antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        return services;
    }

    public static IApplicationBuilder UseIdentityServices(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseCurrentTenant();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapAntiforgeryBootstrap(this WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new
            {
                token = tokens.RequestToken,
                headerName = "X-CSRF-TOKEN"
            });
        }).AllowAnonymous();

        return app;
    }
}
