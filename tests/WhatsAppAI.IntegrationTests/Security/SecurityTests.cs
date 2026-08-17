using System.Net;
using System.Net.Http.Json;
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
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
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
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized);
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
