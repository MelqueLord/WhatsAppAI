using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Contacts;

[Collection("IntegrationTests")]
public sealed class ContactImportQueueTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Import_binds_selected_queue_from_multipart_form_and_lists_contact_in_queue()
    {
        var owner = await CreateTenantOwnerAsync();
        var queue = ServiceLine.Create(owner.TenantId, "Clientes importados");
        await using (var context = await factory.GetDbContextAsync())
        {
            context.ServiceLines.Add(queue);
            await context.SaveChangesAsync();
        }

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("nome,contato\nAna,5511999990000"));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file, "file", "contacts.csv");
        form.Add(new StringContent(queue.Id.ToString()), "queueId");

        var importResponse = await owner.Client.PostAsync("/api/contacts/import", form);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var importResult = await importResponse.Content.ReadFromJsonAsync<ContactImportResponse>();
        Assert.Equal(1, importResult?.Imported);

        await using (var context = await factory.GetDbContextAsync())
        {
            var importedContact = await context.Contacts.IgnoreQueryFilters()
                .SingleAsync(contact => contact.TenantId == owner.TenantId && contact.PhoneNumber == "5511999990000");
            Assert.Equal(queue.Id, importedContact.QueueId);
        }

        var queuedContacts = await owner.Client.GetFromJsonAsync<List<QueuedContactResponse>>(
            $"/api/contacts?queueId={queue.Id}&limit=50");
        Assert.Contains(queuedContacts ?? [], contact => contact.PhoneNumber == "5511999990000");
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantOwnerAsync()
    {
        await using var context = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await context.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Contact import {suffix}", $"contact-import-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"contact-import-{suffix}@test.example", "Contact Import Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("ContactImport@123"));
        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();
        context.AddRange(tenant, user, membership);
        await context.SaveChangesAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "ContactImport@123"
        });
        login.EnsureSuccessStatusCode();
        return (client, tenant.Id);
    }

    private sealed record ContactImportResponse(int Imported);

    private sealed record QueuedContactResponse(string PhoneNumber);
}
