namespace AntiFraude.Application.DTOs;

public sealed record TransactionRequest(
    string TransactionId,
    decimal Amount,
    string MerchantId,
    string CustomerId,
    string Currency,
    Dictionary<string, string>? Metadata = null
);
