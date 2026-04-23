using System.Text.Json;
using AntiFraude.Application.DTOs;
using AntiFraude.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AntiFraude.Infrastructure.Messaging;

/// <summary>
/// Background service que periodicamente lê mensagens pendentes do Outbox e as publica
/// no RabbitMQ via MassTransit. Garante entrega "at least once" mesmo em caso de crash
/// entre a gravação no banco e a publicação no broker.
/// </summary>
public sealed class OutboxRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayService> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public OutboxRelayService(IServiceScopeFactory scopeFactory, ILogger<OutboxRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxRelayService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 3)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in pending)
        {
            try
            {
                if (msg.MessageType.Contains(nameof(TransactionSubmittedEvent)))
                {
                    var @event = JsonSerializer.Deserialize<TransactionSubmittedEvent>(msg.Payload);
                    if (@event is not null)
                        await publishEndpoint.Publish(@event, ct);
                }

                msg.ProcessedAt = DateTime.UtcNow;
                _logger.LogDebug("Outbox message {MessageId} published successfully", msg.Id);
            }
            catch (Exception ex)
            {
                msg.RetryCount++;
                msg.Error = ex.Message;
                _logger.LogWarning(ex, "Failed to publish outbox message {MessageId} (attempt {Attempt})", msg.Id, msg.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
