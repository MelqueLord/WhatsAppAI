using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Persistence.Repositories;
using WhatsAppAI.Infrastructure.Workers;

namespace WhatsAppAI.UnitTests.Workers;

public sealed class WhatsAppTemplateSubmissionWorkerTests
{
    [Fact]
    public async Task ProcessSubmissionAsync_AcceptsTheSubmissionAfterMetaAcceptsIt()
    {
        await using var fixture = await TemplateFixture.CreateAsync(new WhatsAppTemplateCreateResult
        {
            IsSuccess = true,
            MetaTemplateId = "meta-template",
            Status = "PENDING",
            Category = "UTILITY"
        });

        await WhatsAppTemplateSubmissionWorker.ProcessSubmissionAsync(fixture.Submission, fixture.Services, CancellationToken.None);

        Assert.Equal(WhatsAppTemplateSubmissionStatus.Accepted, fixture.Submission.Status);
        Assert.Equal("meta-template", fixture.Template.MetaTemplateId);
        Assert.Equal(1, fixture.Submission.AttemptCount);
    }

    [Fact]
    public async Task ProcessSubmissionAsync_SchedulesRetryForTransientMetaFailure()
    {
        await using var fixture = await TemplateFixture.CreateAsync(new WhatsAppTemplateCreateResult
        {
            FailureKind = WhatsAppTemplateFailureKind.Transient,
            ErrorCode = "500",
            ErrorMessage = "temporary"
        });

        await WhatsAppTemplateSubmissionWorker.ProcessSubmissionAsync(fixture.Submission, fixture.Services, CancellationToken.None);

        Assert.Equal(WhatsAppTemplateSubmissionStatus.RetryScheduled, fixture.Submission.Status);
        Assert.Equal("500", fixture.Submission.LastErrorCode);
        Assert.NotNull(fixture.Submission.NextAttemptAt);
    }

    private sealed class TemplateFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ServiceProvider provider;
        public IServiceProvider Services => provider;
        public WhatsAppMessageTemplate Template { get; }
        public WhatsAppTemplateSubmission Submission { get; }

        private TemplateFixture(SqliteConnection connection, ServiceProvider provider, WhatsAppMessageTemplate template,
            WhatsAppTemplateSubmission submission)
        {
            this.connection = connection;
            this.provider = provider;
            Template = template;
            Submission = submission;
        }

        public static async Task<TemplateFixture> CreateAsync(WhatsAppTemplateCreateResult result)
        {
            var databaseConnection = new SqliteConnection("Data Source=:memory:");
            await databaseConnection.OpenAsync();
            var tenantId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(databaseConnection).Options;
            var context = new AppDbContext(options, new CurrentTenant());
            await context.Database.EnsureCreatedAsync();

            var account = WhatsAppAccount.Create(tenantId, "waba-1", "phone-1", "secret-1");
            var waba = WhatsAppBusinessAccount.Create(tenantId, "waba-1");
            account.AssignBusinessAccount(waba.Id);
            var template = WhatsAppMessageTemplate.CreatePending(tenantId, waba.Id, "atualizacao", "pt_BR", "UTILITY",
                "Olá, {{1}}.", null, ["Maria"], 1);
            var submission = WhatsAppTemplateSubmission.Queue(tenantId, waba.Id, template.Id, account.Id,
                "idempotency-key", new string('a', 64), "correlation");
            context.AddRange(waba, account, template, submission);
            await context.SaveChangesAsync();

            var services = new ServiceCollection()
                .AddSingleton<IWhatsAppTemplateRepository>(new WhatsAppTemplateRepository(context))
                .AddSingleton<ISecretStore>(new SecretStore())
                .AddSingleton<IWhatsAppClientResolver>(new ClientResolver(new TemplateClient(result)))
                .BuildServiceProvider();
            return new TemplateFixture(databaseConnection, services, template, submission);
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class CurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
        public Guid? UserId => null;
        public string? UserRole => null;
        public bool IsPlatformAdmin => false;
        public bool IsAuthenticated => false;
        public SupportSessionInfo? SupportSession => null;
        public void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin) { }
        public void EnterSupportSession(Guid tenantId, string reason) { }
        public void ExitSupportSession() { }
        public void Clear() { }
    }

    private sealed class SecretStore : ISecretStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<string?>("token");
        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class ClientResolver(IWhatsAppClient client) : IWhatsAppClientResolver
    {
        public IWhatsAppClient GetClient(WhatsAppConnectionType connectionType) => client;
    }

    private sealed class TemplateClient(WhatsAppTemplateCreateResult result) : IWhatsAppClient
    {
        public Task<WhatsAppTemplateCreateResult> CreateTemplateAsync(string wabaId, string accessToken, WhatsAppTemplateCreateRequest template, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<WhatsAppConnectionResult> TestConnectionAsync(string phoneNumberId, string accessToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SendMessageResult> SendTextMessageAsync(string phoneNumberId, string accessToken, string recipientPhone, string text, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SendMessageResult> SendMediaMessageAsync(string phoneNumberId, string accessToken, string recipientPhone, Stream mediaStream, string contentType, long contentLength, string contentSha256, string? caption, string? fileName, string? idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SendMessageResult> SendTemplateMessageAsync(string phoneNumberId, string accessToken, string recipientPhone, string templateName, string templateLanguage, IReadOnlyList<string> parameters, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WhatsAppTemplateListResult> ListTemplatesAsync(string wabaId, string accessToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WhatsAppQrCodeResult> GetQrCodeAsync(Guid tenantId, int lineNumber = 1, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WhatsAppSessionStatus> GetSessionStatusAsync(Guid tenantId, int lineNumber = 1, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DisconnectSessionAsync(Guid tenantId, int lineNumber = 1, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
