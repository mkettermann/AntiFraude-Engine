using AntiFraude.Domain.Enums;

namespace AntiFraude.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public TransactionStatus FromStatus { get; private set; }
    public TransactionStatus ToStatus { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string Message { get; private set; } = string.Empty;

    // EF Core constructor
    private AuditLog() { }

    public static AuditLog Create(
        Guid transactionId,
        TransactionStatus fromStatus,
        TransactionStatus toStatus,
        string message)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            OccurredAt = DateTime.UtcNow,
            Message = message
        };
    }
}
