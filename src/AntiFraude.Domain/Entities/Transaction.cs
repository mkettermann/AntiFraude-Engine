using AntiFraude.Domain.Enums;

namespace AntiFraude.Domain.Entities;

public class Transaction
{
    public Guid Id { get; private set; }
    public string TransactionId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string MerchantId { get; private set; } = string.Empty;
    public string CustomerId { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public TransactionStatus Status { get; private set; }
    public TransactionDecision? Decision { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Dictionary<string, string>? Metadata { get; private set; }

    // EF Core constructor
    private Transaction() { }

    public static Transaction Create(
        string transactionId,
        decimal amount,
        string merchantId,
        string customerId,
        string currency,
        Dictionary<string, string>? metadata = null)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            Amount = amount,
            MerchantId = merchantId,
            CustomerId = customerId,
            Currency = currency,
            Status = TransactionStatus.RECEIVED,
            CreatedAt = DateTime.UtcNow,
            Metadata = metadata
        };
    }

    public void MarkAsProcessing()
    {
        Status = TransactionStatus.PROCESSING;
    }

    public void ApplyDecision(TransactionDecision decision, string? reason = null)
    {
        Status = decision switch
        {
            TransactionDecision.APPROVED => TransactionStatus.APPROVED,
            TransactionDecision.REJECTED => TransactionStatus.REJECTED,
            TransactionDecision.REVIEW   => TransactionStatus.REVIEW,
            _ => throw new InvalidOperationException($"Unknown decision: {decision}")
        };

        Decision = decision;
        Reason = reason;
        ProcessedAt = DateTime.UtcNow;
    }
}
