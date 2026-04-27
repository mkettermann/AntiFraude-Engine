using System.Diagnostics;
using AntiFraude.Application.Interfaces;
using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Enums;
using AntiFraude.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AntiFraude.Application.Services;

/// <summary>
/// Orquestra todas as <see cref="IFraudRule"/> registradas no contêiner DI e produz a decisão final.
///
/// As regras são injetadas como IEnumerable&lt;IFraudRule&gt; — cada uma registrada como Transient,
/// o que garante que cada avaliação use instâncias isoladas sem compartilhamento de estado.
/// </summary>
public sealed class FraudEvaluationService : IFraudEvaluationService
{
    private readonly IEnumerable<IFraudRule> _rules;
    private readonly ILogger<FraudEvaluationService> _logger;

    public FraudEvaluationService(
        IEnumerable<IFraudRule> rules,
        ILogger<FraudEvaluationService> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task<(TransactionDecision Decision, string? Reason)> EvaluateAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting fraud evaluation for TransactionId {TransactionId} with {RuleCount} rules",
            transaction.TransactionId, _rules.Count());

        foreach (var rule in _rules)
        {
            var ruleTimer = Stopwatch.StartNew();
            var result = await rule.EvaluateAsync(transaction, cancellationToken);
            ruleTimer.Stop();

            _logger.LogInformation(
                "Rule {RuleName} evaluated TransactionId {TransactionId}: IsRejected={IsRejected} Reason={Reason} in {Duration}ms",
                rule.RuleName, transaction.TransactionId, result.IsRejected, result.Reason, ruleTimer.ElapsedMilliseconds);

            if (result.IsRejected)
            {
                sw.Stop();
                _logger.LogWarning(
                    "TransactionId {TransactionId} REJECTED by rule {RuleName} after {Duration}ms. Reason: {Reason}",
                    transaction.TransactionId, rule.RuleName, sw.ElapsedMilliseconds, result.Reason);

                return (TransactionDecision.REJECTED, result.Reason);
            }
        }

        sw.Stop();
        _logger.LogInformation(
            "TransactionId {TransactionId} passed all fraud rules — APPROVED in {Duration}ms",
            transaction.TransactionId, sw.ElapsedMilliseconds);

        return (TransactionDecision.APPROVED, null);
    }
}
