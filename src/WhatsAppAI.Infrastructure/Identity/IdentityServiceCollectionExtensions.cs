using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Identity;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var requireSecureCookies = environment.IsProduction();

        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            keysPath = environment.IsProduction()
                ? "/home/app/.aspnet/DataProtection-Keys"
                : Path.Combine(AppContext.BaseDirectory, "DataProtection-Keys");
        }

        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("WhatsAppAI")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        if (environment.IsProduction())
        {
            var certificatePath = configuration["DataProtection:CertificatePath"];
            var certificatePassword = configuration["DataProtection:CertificatePassword"];
            var certificateBase64 = configuration["DataProtection:CertificateBase64"];
            var allowUnencryptedKeys = configuration.GetValue<bool>(
                "DataProtection:AllowUnencryptedKeys");

            if (string.IsNullOrWhiteSpace(certificatePassword) ||
                (string.IsNullOrWhiteSpace(certificatePath) && string.IsNullOrWhiteSpace(certificateBase64)))
            {
                if (allowUnencryptedKeys)
                {
                    Console.Error.WriteLine(
                        "WARNING: Data Protection keys are not encrypted with a certificate. This mode is for temporary testing only.");
                }
                else
                {
                throw new InvalidOperationException(
                    "DataProtection:CertificatePassword and either DataProtection:CertificatePath or DataProtection:CertificateBase64 are required in production.");
                }
            }
            else
            {
                X509Certificate2 certificate;
                if (!string.IsNullOrWhiteSpace(certificateBase64))
                {
                    try
                    {
                        certificate = X509CertificateLoader.LoadPkcs12(
                            Convert.FromBase64String(certificateBase64),
                            certificatePassword,
                            X509KeyStorageFlags.EphemeralKeySet);
                    }
                    catch (FormatException ex)
                    {
                        throw new InvalidOperationException(
                            "DataProtection:CertificateBase64 is not valid Base64.", ex);
                    }
                }
                else
                {
                    if (!File.Exists(certificatePath))
                    {
                        throw new InvalidOperationException(
                            "DataProtection certificate file was not found at the configured path.");
                    }

                    certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        certificatePath,
                        certificatePassword,
                        X509KeyStorageFlags.EphemeralKeySet);
                }

                if (!certificate.HasPrivateKey)
                {
                    throw new InvalidOperationException(
                        "DataProtection certificate must include a private key.");
                }

                dataProtection.ProtectKeysWithCertificate(certificate);
            }
        }

        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IAuthorizationHandler, TenantContextHandler>();

        var jwtSecret = configuration["Jwt:Secret"] ?? string.Empty;
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "whatsappai";
        var jwtAudience = configuration["Jwt:Audience"] ?? "whatsappai";

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = "Smart";
                options.DefaultChallengeScheme = "Smart";
            })
            .AddPolicyScheme("Smart", "Cookie or Bearer", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var auth = context.Request.Headers.Authorization.ToString();
                    if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return JwtBearerDefaults.AuthenticationScheme;
                    return CookieAuthenticationDefaults.AuthenticationScheme;
                };
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/api/auth/login";
                options.LogoutPath = "/api/auth/logout";
                options.AccessDeniedPath = "/api/auth/access-denied";

                options.Cookie.HttpOnly = true;

                options.Cookie.SecurePolicy = requireSecureCookies
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;

                options.Cookie.SameSite = requireSecureCookies
                    ? SameSiteMode.None
                    : SameSiteMode.Lax;

                options.Cookie.Name = "whatsappai.session.v2";

                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;

                options.Events.OnValidatePrincipal = async context =>
                {
                    var authenticationService = context.HttpContext.RequestServices
                        .GetRequiredService<IAuthenticationService>();

                    if (context.Principal is null ||
                        !await authenticationService.ValidatePrincipalAsync(
                            context.Principal,
                            context.HttpContext.RequestAborted))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret)),
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };
                // Do not redirect on 401 — return JSON
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                };
            });

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                "RequireTenantContext",
                policy =>
                    policy
                        .RequireAuthenticatedUser()
                        .AddRequirements(new TenantContextRequirement()))
            .AddPolicy(
                "PlatformAdmin",
                policy =>
                    policy.RequireClaim("platform_admin", "true"));

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";

            options.Cookie.Name = "whatsappai.antiforgery.v2";
            options.Cookie.HttpOnly = true;

            options.Cookie.SecurePolicy = requireSecureCookies
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;

            options.Cookie.SameSite = requireSecureCookies
                ? SameSiteMode.None
                : SameSiteMode.Lax;
        });

        return services;
    }

    public static IApplicationBuilder UseIdentityServices(
        this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseCurrentTenant();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapAntiforgeryBootstrap(
        this WebApplication app)
    {
        app.MapGet(
            "/api/auth/csrf",
            (
                IAntiforgery antiforgery,
                HttpContext httpContext,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger(
                    "AntiforgeryBootstrap");

                try
                {
                    logger.LogInformation(
                        "Generating antiforgery token for request from {Origin}",
                        httpContext.Request.Headers.Origin.ToString());

                    var tokens =
                        antiforgery.GetAndStoreTokens(httpContext);

                    logger.LogInformation(
                        "Antiforgery token generated successfully.");

                    return Results.Ok(new
                    {
                        token = tokens.RequestToken,
                        headerName = "X-CSRF-TOKEN"
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to generate antiforgery token.");

                    return Results.Problem(
                        title: "Failed to generate CSRF token",
                        detail: "Unable to generate CSRF token.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }
            })
            .AllowAnonymous();

        return app;
    }
}
