using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Messaging;

[Collection("IntegrationTests")]
public sealed class ConversationLifecycleEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task ForwardMessage_UsesStoredSourceAndRejectsAnotherTenant()
    {
        var owner = await CreateTenantOwnerAsync();
        var otherOwner = await CreateTenantOwnerAsync();
        var originContact = Contact.Create(owner.TenantId, "5511999999921", "Origin");
        var targetContact = Contact.Create(owner.TenantId, "5511999999922", "Target");
        var otherContact = Contact.Create(otherOwner.TenantId, "5511999999923", "Other");
        var originConversation = Conversation.Create(owner.TenantId, originContact.Id, "manual");
        var targetConversation = Conversation.Create(owner.TenantId, targetContact.Id, "manual");
        targetConversation.RenewWindow();
        var otherConversation = Conversation.Create(otherOwner.TenantId, otherContact.Id, "manual");
        var origin = Message.CreateInbound(owner.TenantId, originConversation.Id, originContact.Id,
            "forward-source", MessageType.Text, "Texto original");
        var other = Message.CreateInbound(otherOwner.TenantId, otherConversation.Id, otherContact.Id,
            "other-forward-source", MessageType.Text, "Segredo de outro tenant");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.AddRange(originContact, targetContact, otherContact);
            db.Conversations.AddRange(originConversation, targetConversation, otherConversation);
            db.Messages.AddRange(origin, other);
            await db.SaveChangesAsync();
        }

        var denied = await owner.Client.PostAsJsonAsync($"/api/conversations/{targetConversation.Id}/messages",
            new { content = "Texto adulterado", forwardSourceMessageId = other.Id });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);

        var accepted = await owner.Client.PostAsJsonAsync($"/api/conversations/{targetConversation.Id}/messages",
            new { content = "Texto adulterado", forwardSourceMessageId = origin.Id });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        await using var verification = await factory.GetDbContextAsync();
        var forwarded = await verification.Messages.IgnoreQueryFilters()
            .SingleAsync(message => message.TenantId == owner.TenantId &&
                message.ConversationId == targetConversation.Id);
        Assert.Equal("Texto original", forwarded.Content);
        Assert.Equal(origin.Id, forwarded.ForwardedFromMessageId);
    }

    [Fact]
    public async Task CorrectMessage_CreatesQuotedCorrectionAndPreservesOriginal()
    {
        var owner = await CreateTenantOwnerAsync();
        var contact = Contact.Create(owner.TenantId, "5511999999924", "Correction target");
        var conversation = Conversation.Create(owner.TenantId, contact.Id, "manual");
        conversation.RenewWindow();
        var original = Message.CreateOutbound(owner.TenantId, conversation.Id, contact.Id,
            MessageType.Text, "Valor antigo", "original-idem");
        original.MarkSent("original-provider-id");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.Add(contact);
            db.Conversations.Add(conversation);
            db.Messages.Add(original);
            await db.SaveChangesAsync();
        }

        var response = await owner.Client.PostAsJsonAsync($"/api/conversations/{conversation.Id}/messages",
            new { content = "Valor corrigido", correctsMessageId = original.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verification = await factory.GetDbContextAsync();
        var messages = await verification.Messages.IgnoreQueryFilters()
            .Where(item => item.TenantId == owner.TenantId && item.ConversationId == conversation.Id)
            .ToListAsync();
        Assert.Equal("Valor antigo", messages.Single(item => item.Id == original.Id).Content);
        var correction = messages.Single(item => item.Id != original.Id);
        Assert.Equal("Correção: Valor corrigido", correction.Content);
        Assert.Equal(original.Id, correction.CorrectsMessageId);
        Assert.Equal("original-provider-id", correction.QuotedMessageId);
    }

    [Fact]
    public async Task SearchMessages_IsScopedToAuthorizedConversation()
    {
        var owner = await CreateTenantOwnerAsync();
        var otherOwner = await CreateTenantOwnerAsync();
        var ownContact = Contact.Create(owner.TenantId, "5511999999925", "Own search");
        var otherContact = Contact.Create(otherOwner.TenantId, "5511999999926", "Other search");
        var ownConversation = Conversation.Create(owner.TenantId, ownContact.Id, "manual");
        var otherConversation = Conversation.Create(otherOwner.TenantId, otherContact.Id, "manual");
        var ownMessage = Message.CreateInbound(owner.TenantId, ownConversation.Id, ownContact.Id,
            "own-search-id", MessageType.Text, "Pedido azul");
        var otherMessage = Message.CreateInbound(otherOwner.TenantId, otherConversation.Id, otherContact.Id,
            "other-search-id", MessageType.Text, "Pedido azul secreto");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.AddRange(ownContact, otherContact);
            db.Conversations.AddRange(ownConversation, otherConversation);
            db.Messages.AddRange(ownMessage, otherMessage);
            await db.SaveChangesAsync();
        }

        var result = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/conversations/{ownConversation.Id}/messages?search=azul");
        Assert.Equal(ownMessage.Id, result.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Single(result.GetProperty("items").EnumerateArray());

        var forbidden = await owner.Client.GetAsync(
            $"/api/conversations/{otherConversation.Id}/messages?search=azul");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    [Fact]
    public async Task ReactToMessage_RequiresAnAuthorizedTargetAndQueuesReaction()
    {
        var owner = await CreateTenantOwnerAsync();
        var otherOwner = await CreateTenantOwnerAsync();
        var contact = Contact.Create(owner.TenantId, "5511999999927", "Reaction target");
        var otherContact = Contact.Create(otherOwner.TenantId, "5511999999928", "Other reaction target");
        var conversation = Conversation.Create(owner.TenantId, contact.Id, "manual");
        conversation.RenewWindow();
        var otherConversation = Conversation.Create(otherOwner.TenantId, otherContact.Id, "manual");
        var target = Message.CreateInbound(owner.TenantId, conversation.Id, contact.Id,
            "reaction-target", MessageType.Text, "Olá");
        var otherTarget = Message.CreateInbound(otherOwner.TenantId, otherConversation.Id, otherContact.Id,
            "other-reaction-target", MessageType.Text, "Segredo");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.AddRange(contact, otherContact);
            db.Conversations.AddRange(conversation, otherConversation);
            db.Messages.AddRange(target, otherTarget);
            await db.SaveChangesAsync();
        }

        var rejected = await owner.Client.PostAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages/{otherTarget.Id}/reactions",
            new { emoji = "👍" });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var accepted = await owner.Client.PostAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages/{target.Id}/reactions",
            new { emoji = "👍" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        await using var verification = await factory.GetDbContextAsync();
        var reaction = await verification.Messages.IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == owner.TenantId && item.Type == MessageType.Reaction);
        Assert.Equal("reaction-target", reaction.QuotedMessageId);
        Assert.Equal("👍", reaction.Content);
    }

    [Fact]
    public async Task DeleteFromInbox_HidesContentWithoutCrossTenantAccess()
    {
        var owner = await CreateTenantOwnerAsync();
        var otherOwner = await CreateTenantOwnerAsync();
        var contact = Contact.Create(owner.TenantId, "5511999999929", "Delete target");
        var otherContact = Contact.Create(otherOwner.TenantId, "5511999999930", "Other delete target");
        var conversation = Conversation.Create(owner.TenantId, contact.Id, "manual");
        var otherConversation = Conversation.Create(otherOwner.TenantId, otherContact.Id, "manual");
        var target = Message.CreateOutbound(owner.TenantId, conversation.Id, contact.Id,
            MessageType.Text, "Conteúdo próprio", "delete-target-idempotency");
        var customerMessage = Message.CreateInbound(owner.TenantId, conversation.Id, contact.Id,
            "customer-delete-target", MessageType.Text, "Mensagem do cliente");
        var otherTarget = Message.CreateInbound(otherOwner.TenantId, otherConversation.Id, otherContact.Id,
            "other-delete-target", MessageType.Text, "Outro conteúdo");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.AddRange(contact, otherContact);
            db.Conversations.AddRange(conversation, otherConversation);
            db.Messages.AddRange(target, customerMessage, otherTarget);
            await db.SaveChangesAsync();
        }

        var forbidden = await owner.Client.DeleteAsync(
            $"/api/conversations/{otherConversation.Id}/messages/{otherTarget.Id}?scope=local");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);

        var customerDelete = await owner.Client.DeleteAsync(
            $"/api/conversations/{conversation.Id}/messages/{customerMessage.Id}?scope=local");
        Assert.Equal(HttpStatusCode.Forbidden, customerDelete.StatusCode);

        var deleted = await owner.Client.DeleteAsync(
            $"/api/conversations/{conversation.Id}/messages/{target.Id}?scope=local");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var history = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/conversations/{conversation.Id}/messages");
        var visibleMessage = history.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == target.Id);
        Assert.True(visibleMessage.GetProperty("isDeleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, visibleMessage.GetProperty("content").ValueKind);

        var preservedCustomerMessage = history.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == customerMessage.Id);
        Assert.False(preservedCustomerMessage.GetProperty("isDeleted").GetBoolean());
        Assert.Equal("Mensagem do cliente", preservedCustomerMessage.GetProperty("content").GetString());

        var search = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/conversations/{conversation.Id}/messages?search=próprio");
        Assert.Empty(search.GetProperty("items").EnumerateArray());

        await using var verification = await factory.GetDbContextAsync();
        var stored = await verification.Messages.IgnoreQueryFilters().SingleAsync(item => item.Id == target.Id);
        Assert.Equal("Conteúdo privado", stored.Content);
        Assert.NotNull(stored.HiddenFromInboxAt);
    }

    [Fact]
    public async Task NativeEdit_RejectsOfficialLineAndAnotherTenant()
    {
        var owner = await CreateTenantOwnerAsync();
        var otherOwner = await CreateTenantOwnerAsync();
        var contact = Contact.Create(owner.TenantId, "5511999999931", "Official edit target");
        var otherContact = Contact.Create(otherOwner.TenantId, "5511999999932", "Other edit target");
        var conversation = Conversation.Create(owner.TenantId, contact.Id, "official-edit-line");
        var foreignQrConversation = Conversation.Create(owner.TenantId, contact.Id,
            $"qr:{otherOwner.TenantId:D}:1");
        var otherConversation = Conversation.Create(otherOwner.TenantId, otherContact.Id, "official-other-line");
        var message = Message.CreateOutbound(owner.TenantId, conversation.Id, contact.Id,
            MessageType.Text, "Original", "edit-idem");
        message.MarkSent("provider-edit-id");
        var foreignQrMessage = Message.CreateOutbound(owner.TenantId, foreignQrConversation.Id,
            contact.Id, MessageType.Text, "QR original", "foreign-qr-edit-idem");
        foreignQrMessage.MarkSent("foreign-qr-provider-edit-id");
        var otherMessage = Message.CreateOutbound(otherOwner.TenantId, otherConversation.Id,
            otherContact.Id, MessageType.Text, "Other original", "other-edit-idem");
        otherMessage.MarkSent("other-provider-edit-id");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.AddRange(contact, otherContact);
            db.Conversations.AddRange(conversation, foreignQrConversation, otherConversation);
            db.Messages.AddRange(message, foreignQrMessage, otherMessage);
            await db.SaveChangesAsync();
        }

        var forbidden = await owner.Client.PatchAsJsonAsync(
            $"/api/conversations/{otherConversation.Id}/messages/{otherMessage.Id}",
            new { content = "Changed" });
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);

        var unsupported = await owner.Client.PatchAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages/{message.Id}",
            new { content = "Changed" });
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);

        var foreignQr = await owner.Client.PatchAsJsonAsync(
            $"/api/conversations/{foreignQrConversation.Id}/messages/{foreignQrMessage.Id}",
            new { content = "Changed" });
        Assert.Equal(HttpStatusCode.BadRequest, foreignQr.StatusCode);

        await using var verification = await factory.GetDbContextAsync();
        var stored = await verification.Messages.IgnoreQueryFilters().SingleAsync(item => item.Id == message.Id);
        Assert.Equal("Original", stored.Content);
        Assert.Null(stored.EditedAt);
    }

    [Fact]
    public async Task SendImage_DoesNotAllowAnotherTenantConversation()
    {
        var requestingTenant = await CreateTenantOwnerAsync();
        var otherTenant = await CreateTenantOwnerAsync();
        var contact = Contact.Create(otherTenant.TenantId, "5511999999910", "Other Tenant");
        var conversation = Conversation.Create(otherTenant.TenantId, contact.Id, "official-phone-other");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.Add(contact);
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        using var form = new MultipartFormDataContent();
        using var image = new ByteArrayContent([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(image, "file", "tenant-isolation.png");

        var response = await requestingTenant.Client.PostAsync(
            $"/api/conversations/{conversation.Id}/media", form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendImage_UsesDetectedContentTypeWhenMultipartTypeIsIncorrect()
    {
        var setup = await CreateTenantOwnerAsync();
        var contact = Contact.Create(setup.TenantId, "5511999999911", "Image Recipient");
        var conversation = Conversation.Create(setup.TenantId, contact.Id, "manual");
        conversation.RenewWindow();

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.Add(contact);
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        using var form = new MultipartFormDataContent();
        using var image = new ByteArrayContent([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "own-conversation.png");

        var response = await setup.Client.PostAsync(
            $"/api/conversations/{conversation.Id}/media", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TemplateAvailability_FollowsTheConversationOfficialLine()
    {
        var setup = await CreateTenantOwnerAsync();
        var manualContact = Contact.Create(setup.TenantId, "5511999999901", "Legado QR");
        var officialContact = Contact.Create(setup.TenantId, "5511999999902", "Oficial");
        var manualConversation = Conversation.Create(setup.TenantId, manualContact.Id, "manual");
        var officialConversation = Conversation.Create(setup.TenantId, officialContact.Id, "official-phone-1");
        var officialAccount = WhatsAppAccount.Create(setup.TenantId, "waba-1", "official-phone-1", "secret-ref");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.AddRange(manualContact, officialContact);
            db.Conversations.AddRange(manualConversation, officialConversation);
            db.WhatsAppAccounts.Add(officialAccount);
            await db.SaveChangesAsync();
        }

        var list = await setup.Client.GetFromJsonAsync<JsonElement>("/api/conversations");
        var items = list.GetProperty("items").EnumerateArray().ToList();
        var manualItem = items.Single(item => item.GetProperty("id").GetGuid() == manualConversation.Id);
        var officialItem = items.Single(item => item.GetProperty("id").GetGuid() == officialConversation.Id);
        Assert.False(manualItem.GetProperty("canUseTemplates").GetBoolean());
        Assert.True(officialItem.GetProperty("canUseTemplates").GetBoolean());

        var manualDetail = await setup.Client.GetFromJsonAsync<JsonElement>($"/api/conversations/{manualConversation.Id}");
        Assert.False(manualDetail.GetProperty("canUseTemplates").GetBoolean());

        await using (var db = await factory.GetDbContextAsync())
        {
            var account = await db.WhatsAppAccounts.SingleAsync(item => item.Id == officialAccount.Id);
            account.Deactivate();
            await db.SaveChangesAsync();
        }

        var inactiveDetail = await setup.Client.GetFromJsonAsync<JsonElement>($"/api/conversations/{officialConversation.Id}");
        Assert.False(inactiveDetail.GetProperty("canUseTemplates").GetBoolean());
    }

    [Fact]
    public async Task CloseMovesConversationToClosedFilterAndKeepsHistory()
    {
        var setup = await CreateTenantOwnerAsync();
        var contact = Contact.Create(setup.TenantId, "5511999999999", "Lifecycle Contact");
        var conversation = Conversation.Create(setup.TenantId, contact.Id, "manual");
        var inbound = Message.CreateInbound(
            setup.TenantId,
            conversation.Id,
            contact.Id,
            "lifecycle-inbound",
            MessageType.Text,
            "Olá, preciso de ajuda");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.Add(contact);
            db.Conversations.Add(conversation);
            db.Messages.Add(inbound);
            await db.SaveChangesAsync();
        }

        using var closeRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/conversations/{conversation.Id}/close");
        closeRequest.Headers.TryAddWithoutValidation("If-Match", "1");
        using var close = await setup.Client.SendAsync(closeRequest);

        var openList = await setup.Client.GetFromJsonAsync<JsonElement>(
            "/api/conversations?status=Open");
        var closedList = await setup.Client.GetFromJsonAsync<JsonElement>(
            "/api/conversations?status=Closed");
        var history = await setup.Client.GetFromJsonAsync<JsonElement>(
            $"/api/conversations/{conversation.Id}/messages");

        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Empty(openList.GetProperty("items").EnumerateArray());
        Assert.Single(closedList.GetProperty("items").EnumerateArray());
        Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal("Olá, preciso de ajuda",
            history.GetProperty("items")[0].GetProperty("content").GetString());
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantOwnerAsync()
    {
        await using var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Conversation lifecycle {suffix}", $"conversation-lifecycle-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"conversation-lifecycle-{suffix}@test.example", "Lifecycle Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("ConversationLifecycle@123"));
        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();
        db.Tenants.Add(tenant);
        db.Users.Add(user);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "ConversationLifecycle@123"
        });
        login.EnsureSuccessStatusCode();
        return (client, tenant.Id);
    }
}
