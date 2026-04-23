using System.Diagnostics;
using AntiFraude.Application.DTOs;
using AntiFraude.Application.Interfaces;
using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Enums;
using AntiFraude.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;
using SerilogContext = Serilog.Context.LogContext;

namespace AntiFraude.Worker.Consumers;

/// <summary>
/// Consome mensagens da fila de transações publicadas pelo Outbox Relay.
///
/// Fluxo: RECEIVED → PROCESSING → APPROVED / REJECTED / REVIEW
/// DLQ: após 3 tentativas malsucedidas, MassTransit encaminha para a Dead Letter Queue.
/// </summary>
public sealed class TransactionConsumer : IConsumer<TransactionSubmittedEvent>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IFraudEvaluationService _fraudEvaluationService;
    private readonly ILogger<TransactionConsumer> _logger;

    public TransactionConsumer(
        ITransactionRepository transactionRepository,
        IAuditLogRepository auditLogRepository,
        IFraudEvaluationService fraudEvaluationService,
        ILogger<TransactionConsumer> logger)
    {
        _transactionRepository = transactionRepository;
        _auditLogRepository = auditLogRepository;
        _fraudEvaluationService = fraudEvaluationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransactionSubmittedEvent> context)
    {
        var @event = context.Message;
        var correlationId = @event.CorrelationId;

        // Enriquece todos os logs deste processamento com correlationId e transactionId
        using (SerilogContext.PushProperty("CorrelationId", correlationId))
        using (SerilogContext.PushProperty("TransactionId", @event.TransactionId))
        {
            var totalTimer = Stopwatch.StartNew();

            _logger.LogInformation(
                "Transaction {TransactionId} RECEIVED by worker — Amount={Amount} Currency={Currency}",
                @event.TransactionId, @event.Amount, @event.Currency);

            var transaction = await _transactionRepository.GetByIdAsync(@event.InternalId, context.CancellationToken);

            if (transaction is null)
            {
                _logger.LogError(
                    "Transaction {TransactionId} (InternalId={InternalId}) not found in database — skipping",
                    @event.TransactionId, @event.InternalId);
                return;
            }

            // ── Transição RECEIVED → PROCESSING ──────────────────────────────────
            var previousStatus = transaction.Status;
            transaction.MarkAsProcessing();
            await _transactionRepository.UpdateAsync(transaction, context.CancellationToken);

            await _auditLogRepository.AddAsync(
                AuditLog.Create(
                    transaction.Id,
                    previousStatus,
                    TransactionStatus.PROCESSING,
                    $"Transaction picked up by worker instance {Environment.MachineName}"),
                context.CancellationToken);

            _logger.LogInformation(
                "Transaction {TransactionId} status: RECEIVED → PROCESSING",
                @event.TransactionId);

            // ── Avaliação pelas regras de fraude ──────────────────────────────────
            var evalTimer = Stopwatch.StartNew();
            var (decision, reason) = await _fraudEvaluationService.EvaluateAsync(
                transaction, context.CancellationToken);
            evalTimer.Stop();

            _logger.LogInformation(
                "Transaction {TransactionId} evaluated as {Decision} in {Duration}ms — Reason: {Reason}",
                @event.TransactionId, decision, evalTimer.ElapsedMilliseconds, reason ?? "N/A");

            // ── Aplicar decisão final e registrar auditoria ────────────────────────
            var statusBeforeDecision = transaction.Status;
            transaction.ApplyDecision(decision, reason);
            await _transactionRepository.UpdateAsync(transaction, context.CancellationToken);

            await _auditLogRepository.AddAsync(
                AuditLog.Create(
                    transaction.Id,
                    statusBeforeDecision,
                    transaction.Status,
                    $"Decision={decision} Reason={reason ?? "N/A"} EvalDuration={evalTimer.ElapsedMilliseconds}ms"),
                context.CancellationToken);

            totalTimer.Stop();

            _logger.LogInformation(
                "Transaction {TransactionId} PROCESSING → {FinalStatus} in {Duration}ms",
                @event.TransactionId, transaction.Status, totalTimer.ElapsedMilliseconds);
        }
    }
}
