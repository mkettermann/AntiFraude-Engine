namespace AntiFraude.Infrastructure.Data;

/// <summary>
/// Mensagem persistida no banco antes de ser publicada no broker (Outbox Pattern).
/// A tabela OutboxMessages é lida por um relay background service que publica no RabbitMQ.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
