using AntiFraude.Domain.Entities;
using AntiFraude.Domain.ValueObjects;

namespace AntiFraude.Domain.Interfaces;

/// <summary>
/// Contrato para cada regra de avaliação antifraude.
///
/// LIFETIME: Transient — cada avaliação recebe uma instância isolada, garantindo
/// que nenhum estado interno (contadores, caches locais, etc.) seja compartilhado
/// entre requisições concorrentes. Regras devem ser stateless ou conter estado
/// exclusivamente por instância.
/// </summary>
public interface IFraudRule
{
    /// <summary>Nome descritivo da regra — usado em logs de auditoria.</summary>
    string RuleName { get; }

    Task<FraudRuleResult> EvaluateAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
