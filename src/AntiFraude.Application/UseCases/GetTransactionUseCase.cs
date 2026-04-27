using AntiFraude.Application.DTOs;
using AntiFraude.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AntiFraude.Application.UseCases;

public sealed class GetTransactionUseCase
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<GetTransactionUseCase> _logger;

    public GetTransactionUseCase(
        ITransactionRepository transactionRepository,
        IAuditLogRepository auditLogRepository,
        ILogger<GetTransactionUseCase> logger)
    {
        _transactionRepository = transactionRepository;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task<TransactionResponse?> ExecuteAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionRepository.GetByTransactionIdAsync(transactionId, cancellationToken);

        if (transaction is null)
        {
            _logger.LogWarning("TransactionId {TransactionId} not found", transactionId);
            return null;
        }

        var auditLogs = await _auditLogRepository.GetByTransactionIdAsync(transaction.Id, cancellationToken);

        var auditTrail = auditLogs
            .OrderBy(a => a.OccurredAt)
            .Select(a => new AuditLogEntry(
                a.FromStatus.ToString(),
                a.ToStatus.ToString(),
                a.OccurredAt,
                a.Message))
            .ToList();

        return new TransactionResponse(
            transaction.TransactionId,
            transaction.Status.ToString(),
            transaction.Decision?.ToString(),
            transaction.Reason,
            transaction.CreatedAt,
            transaction.ProcessedAt,
            auditTrail);
    }
}
