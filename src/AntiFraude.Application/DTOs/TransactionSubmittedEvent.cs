using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Enums;

namespace AntiFraude.Application.DTOs;

public sealed record TransactionSubmittedEvent(
    Guid InternalId,
    string TransactionId,
    decimal Amount,
    string MerchantId,
    string CustomerId,
    string Currency,
    Dictionary<string, string>? Metadata,
    string CorrelationId
);
