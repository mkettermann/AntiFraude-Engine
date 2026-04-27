namespace AntiFraude.Application.DTOs;

public sealed record AuditLogEntry(
    string FromStatus,
    string ToStatus,
    DateTime OccurredAt,
    string Message
);

public sealed record TransactionResponse(
    string TransactionId,
    string Status,
    string? Decision,
    string? Reason,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    IReadOnlyList<AuditLogEntry> AuditTrail
);
