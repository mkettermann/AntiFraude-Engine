namespace AntiFraude.Domain.ValueObjects;

/// <summary>
/// Resultado imutável retornado por cada <see cref="Interfaces.IFraudRule"/>.
/// </summary>
public sealed record FraudRuleResult(bool IsRejected, string? Reason)
{
    public static FraudRuleResult Approved() => new(false, null);
    public static FraudRuleResult Rejected(string reason) => new(true, reason);
}
