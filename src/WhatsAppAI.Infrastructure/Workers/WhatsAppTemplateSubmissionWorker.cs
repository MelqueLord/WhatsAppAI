using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class WhatsAppTemplateSubmissionWorker(
    IServiceProvider serviceProvider,
    ILogger<WhatsAppTemplateSubmissionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                await Task.Delay(processed ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error processing WhatsApp template submissions");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    internal async Task<bool> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWhatsAppTemplateRepository>();
        var submissions = await repository.GetDueSubmissionsAsync(20, DateTime.UtcNow, cancellationToken);
        foreach (var submission in submissions)
            await ProcessSubmissionAsync(submission, scope.ServiceProvider, cancellationToken);
        return submissions.Count > 0;
    }

    internal static async Task ProcessSubmissionAsync(WhatsAppTemplateSubmission submission, IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IWhatsAppTemplateRepository>();
        var now = DateTime.UtcNow;
        if (!submission.TryClaim(now, TimeSpan.FromMinutes(2))) return;
        await repository.SaveChangesAsync(cancellationToken);

        var template = await repository.GetTemplateByIdAsync(submission.WhatsAppMessageTemplateId, cancellationToken);
        var account = await repository.GetSourceAccountAsync(submission.SourceWhatsAppAccountId, cancellationToken);
        var waba = await repository.GetBusinessAccountAsync(submission.TenantId, submission.WhatsAppBusinessAccountId, cancellationToken);
        if (template is null || account is null || waba is null || account.TenantId != submission.TenantId ||
            account.WhatsAppBusinessAccountId != waba.Id || account.ConnectionType != WhatsAppConnectionType.OfficialApi || !account.IsActive)
        {
            submission.MarkPermanentFailure(null, "configuration", "A linha oficial não está disponível para a submissão.", now);
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        var secretStore = services.GetRequiredService<ISecretStore>();
        var token = await secretStore.GetAsync(account.AccessTokenRef, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            submission.MarkPermanentFailure(null, "credential", "A credencial da linha não está disponível.", now);
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        var client = services.GetRequiredService<IWhatsAppClientResolver>().GetClient(WhatsAppConnectionType.OfficialApi);
        if (submission.Status == WhatsAppTemplateSubmissionStatus.Reconciling)
        {
            var listing = await client.ListTemplatesAsync(waba.WabaId, token, cancellationToken);
            if (!listing.IsSuccess)
            {
                submission.ScheduleRetry(null, "reconciliation", listing.ErrorMessage ?? "Não foi possível reconciliar o catálogo.", now);
                await repository.SaveChangesAsync(cancellationToken);
                return;
            }

            var remote = listing.Templates.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, template.Name, StringComparison.Ordinal) &&
                string.Equals(candidate.Language, template.Language, StringComparison.OrdinalIgnoreCase));
            if (remote is not null)
            {
                if (!string.Equals(remote.BodyText, template.BodyText, StringComparison.Ordinal) ||
                    !string.Equals(remote.FooterText ?? string.Empty, template.FooterText ?? string.Empty, StringComparison.Ordinal))
                    submission.MarkNeedsAttention("Existe uma variante remota com conteúdo diferente.", now);
                else
                {
                    await repository.AddOrUpdateFromProviderAsync(submission.TenantId, waba.Id, remote, cancellationToken);
                    submission.MarkAccepted(now);
                }
                await repository.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var examples = System.Text.Json.JsonSerializer.Deserialize<string[]>(template.BodyExamplesJson) ?? [];
        var result = await client.CreateTemplateAsync(waba.WabaId, token,
            new WhatsAppTemplateCreateRequest(template.Name, template.Language, template.RequestedCategory ?? template.EffectiveCategory,
                template.BodyText, examples, template.FooterText), cancellationToken);
        if (result.IsSuccess)
        {
            template.ApplyProviderSnapshot(result.MetaTemplateId, result.Category, result.Status, template.BodyText, template.FooterText,
                template.BodyParameterCount, template.ComponentsJson, now);
            submission.MarkAccepted(now);
        }
        else
        {
            switch (result.FailureKind)
            {
                case WhatsAppTemplateFailureKind.OutcomeUnknown:
                case WhatsAppTemplateFailureKind.Duplicate:
                    submission.MarkOutcomeUnknown(result.ErrorCode, result.ErrorMessage ?? "Resultado incerto.", now);
                    break;
                case WhatsAppTemplateFailureKind.Transient:
                case WhatsAppTemplateFailureKind.RateLimit:
                    submission.ScheduleRetry(result.ErrorCode, result.FailureKind.ToString(), result.ErrorMessage ?? "Falha temporária.", now);
                    break;
                default:
                    submission.MarkPermanentFailure(result.ErrorCode, result.FailureKind.ToString(), result.ErrorMessage ?? "A Meta rejeitou o template.", now);
                    break;
            }
        }
        await repository.SaveChangesAsync(cancellationToken);
    }
}
