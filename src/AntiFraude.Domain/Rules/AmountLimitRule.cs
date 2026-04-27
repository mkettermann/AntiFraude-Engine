using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Interfaces;
using AntiFraude.Domain.ValueObjects;

namespace AntiFraude.Domain.Rules;

/// <summary>
/// Regra obrigatória: transações com Amount > 10.000 são automaticamente rejeitadas.
///
/// LIFETIME: Transient — registrada como transient em DI para que cada avaliação
/// receba uma instância isolada, sem risco de compartilhamento de estado entre
/// requisições concorrentes (ver InfrastructureServiceExtensions.cs).
/// </summary>
public sealed class AmountLimitRule : IFraudRule
{
    private const decimal Limit = 10_000m;

    public string RuleName => "AmountLimitRule";

    public Task<FraudRuleResult> EvaluateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var result = transaction.Amount > Limit
            ? FraudRuleResult.Rejected("AMOUNT_EXCEEDS_LIMIT")
            : FraudRuleResult.Approved();

        return Task.FromResult(result);
    }
}
