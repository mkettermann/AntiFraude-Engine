using AntiFraude.Domain.Entities;

namespace AntiFraude.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
