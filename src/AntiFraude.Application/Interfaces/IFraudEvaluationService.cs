using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Enums;

namespace AntiFraude.Application.Interfaces;

public interface IFraudEvaluationService
{
    /// <summary>
    /// Avalia a transação contra todas as regras registradas e retorna a decisão final.
    /// </summary>
    Task<(TransactionDecision Decision, string? Reason)> EvaluateAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default);
}
