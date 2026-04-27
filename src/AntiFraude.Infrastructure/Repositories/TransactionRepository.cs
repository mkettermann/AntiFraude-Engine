using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Interfaces;
using AntiFraude.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AntiFraude.Infrastructure.Repositories;

// LIFETIME: Scoped — compartilha o mesmo DbContext dentro de um request/mensagem.
public sealed class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _db;

    public TransactionRepository(AppDbContext db) => _db = db;

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Transaction?> GetByTransactionIdAsync(string transactionId, CancellationToken ct = default)
        => _db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId, ct);

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        await _db.Transactions.AddAsync(transaction, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        _db.Transactions.Update(transaction);
        await _db.SaveChangesAsync(ct);
    }
}
