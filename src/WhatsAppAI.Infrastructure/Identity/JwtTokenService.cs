using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Infrastructure.Identity;

public interface IJwtTokenService
{
    string Generate(User user, TenantMembership? membership, bool isPlatformAdmin = false);
}

internal sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string Generate(User user, TenantMembership? membership, bool isPlatformAdmin = false)
    {
        var signingKeyValue = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

#pragma warning disable S6781 // value comes from IConfiguration (vault/env), not hardcoded
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyValue));
#pragma warning restore S6781
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("security_stamp", user.SecurityStamp),
        };

        if (isPlatformAdmin)
        {
            claims.Add(new Claim("platform_admin", "true"));
            claims.Add(new Claim(ClaimTypes.Role, "PlatformAdmin"));
        }
        else
        {
            if (membership is null)
                throw new InvalidOperationException("Tenant users must have a membership.");

            claims.Add(new Claim("tenant_id", membership.TenantId.ToString()));
            claims.Add(new Claim("membership_id", membership.Id.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, membership.Role.ToString()));
        }

        if (user.DisplayName is not null)
            claims.Add(new Claim(ClaimTypes.Name, user.DisplayName));

        var expiry = int.TryParse(configuration["Jwt:ExpiryDays"], out var days) ? days : 30;

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "whatsappai",
            audience: configuration["Jwt:Audience"] ?? "whatsappai",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiry),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
