using AntiFraude.Domain.Entities;

namespace AntiFraude.Domain.Interfaces;

public interface IProcessedMessageRepository
{
    Task<bool> ExistsAsync(string idempotencyKeyHash, CancellationToken cancellationToken = default);
    Task<ProcessedMessage?> GetAsync(string idempotencyKeyHash, CancellationToken cancellationToken = default);
    Task AddAsync(ProcessedMessage message, CancellationToken cancellationToken = default);
}
