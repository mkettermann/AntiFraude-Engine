using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AntiFraude.Application.DTOs;
using AntiFraude.Application.Interfaces;
using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AntiFraude.Application.UseCases;

public sealed class SubmitTransactionUseCase
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IProcessedMessageRepository _processedMessageRepository;
    private readonly IOutboxPublisher _outboxPublisher;
    private readonly ILogger<SubmitTransactionUseCase> _logger;

    public SubmitTransactionUseCase(
        ITransactionRepository transactionRepository,
        IProcessedMessageRepository processedMessageRepository,
        IOutboxPublisher outboxPublisher,
        ILogger<SubmitTransactionUseCase> logger)
    {
        _transactionRepository = transactionRepository;
        _processedMessageRepository = processedMessageRepository;
        _outboxPublisher = outboxPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Retorna (response, isIdempotentReplay):
    /// - isIdempotentReplay=true  → HTTP 200 (já processado, replay da resposta original)
    /// - isIdempotentReplay=false → HTTP 202 (enfileirado para processamento)
    /// </summary>
    public async Task<(TransactionResponse Response, bool IsIdempotentReplay)> ExecuteAsync(
        TransactionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var keyHash = ComputeHash(idempotencyKey);

        // ── Verificação de idempotência ───────────────────────────────────────────
        var existing = await _processedMessageRepository.GetAsync(keyHash, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotent replay for TransactionId {TransactionId} — returning cached response",
                existing.TransactionId);

            var cached = JsonSerializer.Deserialize<TransactionResponse>(existing.ResponsePayload)!;
            return (cached, true);
        }

        // ── Criar transação com status RECEIVED ───────────────────────────────────
        var transaction = Transaction.Create(
            request.TransactionId,
            request.Amount,
            request.MerchantId,
            request.CustomerId,
            request.Currency,
            request.Metadata);

        _logger.LogInformation(
            "Transaction {TransactionId} RECEIVED — Amount={Amount} Currency={Currency}",
            transaction.TransactionId, transaction.Amount, transaction.Currency);

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        // ── Publicar no Outbox (mesma unidade de trabalho) ────────────────────────
        var @event = new TransactionSubmittedEvent(
            transaction.Id,
            transaction.TransactionId,
            transaction.Amount,
            transaction.MerchantId,
            transaction.CustomerId,
            transaction.Currency,
            transaction.Metadata,
            CorrelationContext.Current);

        await _outboxPublisher.PublishAsync(@event, cancellationToken);

        // ── Salvar ProcessedMessage para idempotência futura ──────────────────────
        var response = new TransactionResponse(
            transaction.TransactionId,
            transaction.Status.ToString(),
            null, null,
            transaction.CreatedAt,
            null,
            []);

        var processedMessage = ProcessedMessage.Create(
            keyHash,
            transaction.TransactionId,
            JsonSerializer.Serialize(response));

        await _processedMessageRepository.AddAsync(processedMessage, cancellationToken);

        _logger.LogInformation(
            "Transaction {TransactionId} enqueued for processing",
            transaction.TransactionId);

        return (response, false);
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
