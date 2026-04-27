using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Interfaces;
using AntiFraude.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AntiFraude.Infrastructure.Repositories;

// LIFETIME: Scoped — compartilha o mesmo DbContext dentro de um request/mensagem.
public sealed class ProcessedMessageRepository : IProcessedMessageRepository
{
    private readonly AppDbContext _db;

    public ProcessedMessageRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string idempotencyKeyHash, CancellationToken ct = default)
        => _db.ProcessedMessages.AnyAsync(p => p.IdempotencyKeyHash == idempotencyKeyHash, ct);

    public Task<ProcessedMessage?> GetAsync(string idempotencyKeyHash, CancellationToken ct = default)
        => _db.ProcessedMessages.FirstOrDefaultAsync(p => p.IdempotencyKeyHash == idempotencyKeyHash, ct);

    public async Task AddAsync(ProcessedMessage message, CancellationToken ct = default)
    {
        await _db.ProcessedMessages.AddAsync(message, ct);
        await _db.SaveChangesAsync(ct);
    }
}
