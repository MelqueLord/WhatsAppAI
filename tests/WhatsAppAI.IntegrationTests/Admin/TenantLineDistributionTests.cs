using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace WhatsAppAI.IntegrationTests.Admin;

[Collection("IntegrationTests")]
public sealed class TenantLineDistributionTests(TestWebApplicationFactory factory)
{
    [Theory]
    [InlineData("STAR", 1, 0)]
    [InlineData("STAR", 0, 1)]
    [InlineData("FLOW", 1, 1)]
    [InlineData("FLOW", 0, 2)]
    public async Task CreateTenantAcceptsAnyLineDistributionWithinPlanTotal(
        string planCode,
        int officialApiLineCount,
        int qrCodeLineCount)
    {
        var client = await CreatePlatformAdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsJsonAsync("/api/admin/tenants", new
        {
            Name = $"Lines {suffix}",
            OwnerEmail = $"lines-{suffix}@test.com",
            PlanCode = planCode,
            OfficialApiLineCount = officialApiLineCount,
            QrCodeLineCount = qrCodeLineCount,
            MonthlyAiResponseLimit = 1_500
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(officialApiLineCount, result.GetProperty("officialApiLineCount").GetInt32());
        Assert.Equal(qrCodeLineCount, result.GetProperty("qrCodeLineCount").GetInt32());

        var tenantId = result.GetProperty("tenantId").GetGuid();
        await using var db = await factory.GetDbContextAsync();
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(item => item.Id == tenantId);
        Assert.Equal(officialApiLineCount, tenant.OfficialApiLineCount);
        Assert.Equal(qrCodeLineCount, tenant.QrCodeLineCount);
    }

    [Fact]
    public async Task CreateTenantRejectsLineDistributionOutsidePlanTotal()
    {
        var client = await CreatePlatformAdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsJsonAsync("/api/admin/tenants", new
        {
            Name = $"Invalid lines {suffix}",
            OwnerEmail = $"invalid-lines-{suffix}@test.com",
            PlanCode = "FLOW",
            OfficialApiLineCount = 2,
            QrCodeLineCount = 1,
            MonthlyAiResponseLimit = 5_000
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = await factory.GetDbContextAsync();
        Assert.False(await db.Tenants.IgnoreQueryFilters().AnyAsync(item => item.Name == $"Invalid lines {suffix}"));
    }

    [Fact]
    public async Task UpdateTenantChangesPlanAndPreservesChosenLineDistribution()
    {
        var client = await CreatePlatformAdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"edit-lines-{suffix}@test.com";
        var createResponse = await client.PostAsJsonAsync("/api/admin/tenants", new
        {
            Name = $"Edit lines {suffix}",
            OwnerEmail = ownerEmail,
            PlanCode = "STAR",
            OfficialApiLineCount = 0,
            QrCodeLineCount = 1,
            MonthlyAiResponseLimit = 1_500
        });
        createResponse.EnsureSuccessStatusCode();
        var tenantId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("tenantId").GetGuid();

        uint version;
        await using (var db = await factory.GetDbContextAsync())
        {
            version = (await db.Tenants.IgnoreQueryFilters().SingleAsync(item => item.Id == tenantId)).Version;
        }

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/tenants/{tenantId}")
        {
            Content = JsonContent.Create(new
            {
                Name = $"Edit lines {suffix}",
                OwnerEmail = ownerEmail,
                PlanCode = "FLOW",
                OfficialApiLineCount = 1,
                QrCodeLineCount = 1,
                MonthlyAiResponseLimit = 5_000
            })
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", version.ToString());
        var updateResponse = await client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        await using var verifyDb = await factory.GetDbContextAsync();
        var tenant = await verifyDb.Tenants.IgnoreQueryFilters().SingleAsync(item => item.Id == tenantId);
        Assert.Equal(1, tenant.OfficialApiLineCount);
        Assert.Equal(1, tenant.QrCodeLineCount);
        var flowPlanId = await verifyDb.SubscriptionPlans
            .Where(item => item.Code == "FLOW")
            .Select(item => item.Id)
            .SingleAsync();
        Assert.Equal(flowPlanId, tenant.PlanId);
    }

    private async Task<HttpClient> CreatePlatformAdminClientAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "admin@test.com",
            Password = "Admin@12345!"
        })).EnsureSuccessStatusCode();
        return client;
    }
}
