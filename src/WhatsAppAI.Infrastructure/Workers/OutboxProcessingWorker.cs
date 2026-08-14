using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class OutboxProcessingWorker(
    IServiceProvider serviceProvider,
    ILogger<OutboxProcessingWorker> logger) : BackgroundService
{
    private const int MaxRetries = 5;
    private const int BatchSize = 20;

    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox Processing Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in outbox processing worker");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        logger.LogInformation("Outbox Processing Worker stopped");
    }

    private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var contactRepository = scope.ServiceProvider.GetRequiredService<IContactRepository>();
        var whatsAppAccountRepository = scope.ServiceProvider.GetRequiredService<IWhatsAppAccountRepository>();
        var whatsAppClient = scope.ServiceProvider.GetRequiredService<IWhatsAppClient>();
        var secretStore = scope.ServiceProvider.GetRequiredService<ISecretStore>();

        var pending = await outboxRepository.GetPendingAsync(BatchSize);

        foreach (var outboxMessage in pending)
        {
            await ProcessSingleAsync(
                outboxMessage,
                outboxRepository,
                messageRepository,
                contactRepository,
                whatsAppAccountRepository,
                whatsAppClient,
                secretStore,
                cancellationToken);
        }
    }

    private async Task ProcessSingleAsync(
        OutboxMessage outboxMessage,
        IOutboxMessageRepository outboxRepository,
        IMessageRepository messageRepository,
        IContactRepository contactRepository,
        IWhatsAppAccountRepository whatsAppAccountRepository,
        IWhatsAppClient whatsAppClient,
        ISecretStore secretStore,
        CancellationToken cancellationToken)
    {
        try
        {
            outboxMessage.MarkProcessing();
            await outboxRepository.UpdateAsync(outboxMessage);

            var message = await messageRepository.GetByIdAsync(outboxMessage.MessageId, cancellationToken);
            if (message is null)
            {
                outboxMessage.MarkDead("Message not found");
                await outboxRepository.UpdateAsync(outboxMessage);
                logger.LogWarning("Outbox {OutboxId} references missing message {MessageId}", outboxMessage.Id, outboxMessage.MessageId);
                return;
            }

            var contact = await contactRepository.GetByIdAsync(message.ContactId, cancellationToken);
            if (contact is null)
            {
                outboxMessage.MarkDead("Contact not found");
                await outboxRepository.UpdateAsync(outboxMessage);
                logger.LogWarning("Outbox {OutboxId} references missing contact {ContactId}", outboxMessage.Id, message.ContactId);
                return;
            }

            var account = await whatsAppAccountRepository.GetByTenantAsync(outboxMessage.TenantId, cancellationToken);
            if (account is null || !account.IsActive)
            {
                outboxMessage.MarkDead("WhatsApp account not found or inactive");
                await outboxRepository.UpdateAsync(outboxMessage);
                logger.LogWarning("No active WhatsApp account for tenant {TenantId}", outboxMessage.TenantId);
                return;
            }

            var token = await secretStore.GetAsync(account.AccessTokenRef, cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                outboxMessage.MarkDead("Access token not available");
                await outboxRepository.UpdateAsync(outboxMessage);
                return;
            }

            var result = await whatsAppClient.SendTextMessageAsync(
                account.PhoneNumberId,
                token,
                contact.PhoneNumber,
                message.Content ?? string.Empty,
                cancellationToken);

            if (result.IsSuccess)
            {
                message.MarkSent(result.MessageId ?? string.Empty);
                await messageRepository.UpdateAsync(message, cancellationToken);

                outboxMessage.MarkCompleted();
                await outboxRepository.UpdateAsync(outboxMessage);

                logger.LogInformation("Outbox {OutboxId} completed, message {MessageId} sent", outboxMessage.Id, message.Id);
            }
            else
            {
                HandleFailure(outboxMessage, result.ErrorMessage ?? "Send failed");
                await outboxRepository.UpdateAsync(outboxMessage);
                await messageRepository.UpdateAsync(message, cancellationToken);

                logger.LogWarning("Outbox {OutboxId} failed: {Error}", outboxMessage.Id, outboxMessage.LastError);
            }
        }
        catch (Exception ex)
        {
            HandleFailure(outboxMessage, ex.Message);
            await outboxRepository.UpdateAsync(outboxMessage);
            logger.LogError(ex, "Error processing outbox {OutboxId}", outboxMessage.Id);
        }
    }

    private void HandleFailure(OutboxMessage outboxMessage, string error)
    {
        if (outboxMessage.RetryCount >= MaxRetries)
        {
            outboxMessage.MarkDead(error);
            logger.LogError("Outbox {OutboxId} moved to Dead after {Retries} retries: {Error}",
                outboxMessage.Id, outboxMessage.RetryCount, error);
        }
        else
        {
            var delay = BackoffDelays[Math.Min(outboxMessage.RetryCount, BackoffDelays.Length - 1)];
            outboxMessage.MarkFailed(error, delay);
        }
    }
}
