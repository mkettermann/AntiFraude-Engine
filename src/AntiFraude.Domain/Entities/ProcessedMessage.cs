namespace AntiFraude.Domain.Entities;

public class ProcessedMessage
{
    public Guid Id { get; private set; }

    /// <summary>
    /// SHA-256 hash da Idempotency-Key enviada pelo cliente.
    /// Possui constraint UNIQUE no banco — impede reprocessamento.
    /// </summary>
    public string IdempotencyKeyHash { get; private set; } = string.Empty;

    public string TransactionId { get; private set; } = string.Empty;

    /// <summary>JSON serializado da resposta original para replay sem reprocessamento.</summary>
    public string ResponsePayload { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    // EF Core constructor
    private ProcessedMessage() { }

    public static ProcessedMessage Create(string idempotencyKeyHash, string transactionId, string responsePayload)
    {
        return new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            IdempotencyKeyHash = idempotencyKeyHash,
            TransactionId = transactionId,
            ResponsePayload = responsePayload,
            CreatedAt = DateTime.UtcNow
        };
    }
}
