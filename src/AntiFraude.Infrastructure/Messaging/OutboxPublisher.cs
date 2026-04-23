using System.Text.Json;
using AntiFraude.Application.Interfaces;
using AntiFraude.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace AntiFraude.Infrastructure.Messaging;

/// <summary>
/// Implementação do Outbox Pattern: persiste a mensagem na mesma unidade de trabalho
/// do banco antes de qualquer publicação no broker. Um relay background service
/// (<see cref="OutboxRelayService"/>) lê as mensagens pendentes e as publica no RabbitMQ
/// via MassTransit, marcando-as como processadas.
/// </summary>
// LIFETIME: Scoped — mesmo DbContext do request/mensagem em curso.
public sealed class OutboxPublisher : IOutboxPublisher
{
    private readonly AppDbContext _db;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(AppDbContext db, ILogger<OutboxPublisher> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = typeof(T).FullName ?? typeof(T).Name,
            Payload = JsonSerializer.Serialize(message),
            CreatedAt = DateTime.UtcNow
        };

        await _db.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        // SaveChanges é chamado pelo repositório principal — a gravação é atômica.
        // O relay lê as mensagens onde ProcessedAt IS NULL.
        _logger.LogDebug(
            "Outbox message enqueued: Type={MessageType} Id={MessageId}",
            outboxMessage.MessageType, outboxMessage.Id);
    }
}
