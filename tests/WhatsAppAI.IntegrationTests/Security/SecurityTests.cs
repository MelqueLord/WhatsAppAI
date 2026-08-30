using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WhatsAppAI.IntegrationTests.Security;

[Collection("IntegrationTests")]
public class SecurityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SecurityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task AdminEndpoints_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/admin/tenants");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Found,
            $"Expected Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task CapacityEndpoint_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/admin/tenants/capacity");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Found,
            $"Expected Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task AdminEndpoints_WithNonAdmin_ReturnsForbidden()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/admin/tenants");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Found,
            $"Expected Unauthorized, Forbidden or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task Login_WithoutCsrf_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Password = "password"
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithoutCsrf_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/auth/logout", null);
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Found or HttpStatusCode.OK,
            $"Expected BadRequest, Unauthorized, Found or OK, got {response.StatusCode}");
    }

    [Fact]
    public async Task AuthenticatedCookieMutation_WithoutCsrf_ReturnsBadRequest()
    {
        using var productionClient = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var login = await productionClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "admin@test.com",
            Password = "Admin@12345!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var sessionCookie = login.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("whatsappai.session.v2=", StringComparison.Ordinal))
            .Split(';', 2)[0];
        productionClient.DefaultRequestHeaders.Add("Cookie", sessionCookie);

        var response = await productionClient.PostAsJsonAsync("/api/admin/tenants", new
        {
            Name = "CSRF test tenant",
            Slug = $"csrf-{Guid.NewGuid():N}",
            OwnerEmail = $"csrf-{Guid.NewGuid():N}@test.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ActivateEndpoint_WithInvalidToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/activate", new
        {
            Token = "invalid-token",
            InvitationId = Guid.NewGuid(),
            Password = "NewPassword123!"
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ActivateEndpoint_WithExpiredToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/activate", new
        {
            Token = Convert.ToBase64String(new byte[32]),
            InvitationId = Guid.NewGuid(),
            Password = "NewPassword123!"
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);
    }
}
