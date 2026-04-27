using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Interfaces;
using AntiFraude.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AntiFraude.Infrastructure.Repositories;

// LIFETIME: Scoped — compartilha o mesmo DbContext dentro de um request/mensagem.
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;

    public AuditLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AuditLog auditLog, CancellationToken ct = default)
    {
        await _db.AuditLogs.AddAsync(auditLog, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByTransactionIdAsync(Guid transactionId, CancellationToken ct = default)
    {
        return await _db.AuditLogs
            .Where(a => a.TransactionId == transactionId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync(ct);
    }
}
