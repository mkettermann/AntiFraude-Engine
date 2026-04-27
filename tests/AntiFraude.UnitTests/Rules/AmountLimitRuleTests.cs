using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Rules;
using FluentAssertions;

namespace AntiFraude.UnitTests.Rules;

public class AmountLimitRuleTests
{
    private readonly AmountLimitRule _rule = new();

    private static Transaction CreateTransaction(decimal amount)
        => Transaction.Create("TXN-TEST-001", amount, "MRC-001", "CUS-001", "BRL");

    [Fact]
    public async Task EvaluateAsync_WhenAmountExceedsLimit_ShouldReject()
    {
        // Arrange
        var transaction = CreateTransaction(15_000m);

        // Act
        var result = await _rule.EvaluateAsync(transaction);

        // Assert
        result.IsRejected.Should().BeTrue();
        result.Reason.Should().Be("AMOUNT_EXCEEDS_LIMIT");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAmountEqualsLimit_ShouldNotReject()
    {
        // Arrange — 10.000 exatos NÃO excede o limite (regra é Amount > 10.000)
        var transaction = CreateTransaction(10_000m);

        // Act
        var result = await _rule.EvaluateAsync(transaction);

        // Assert
        result.IsRejected.Should().BeFalse();
        result.Reason.Should().BeNull();
    }

    [Theory]
    [InlineData(10_001)]
    [InlineData(50_000)]
    [InlineData(999_999.99)]
    public async Task EvaluateAsync_WhenAmountAboveLimit_ShouldAlwaysReject(decimal amount)
    {
        // Arrange
        var transaction = CreateTransaction(amount);

        // Act
        var result = await _rule.EvaluateAsync(transaction);

        // Assert
        result.IsRejected.Should().BeTrue();
        result.Reason.Should().Be("AMOUNT_EXCEEDS_LIMIT");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(500)]
    [InlineData(9_999.99)]
    [InlineData(10_000)]
    public async Task EvaluateAsync_WhenAmountBelowOrEqualLimit_ShouldApprove(decimal amount)
    {
        // Arrange
        var transaction = CreateTransaction(amount);

        // Act
        var result = await _rule.EvaluateAsync(transaction);

        // Assert
        result.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void RuleName_ShouldBeAmountLimitRule()
    {
        _rule.RuleName.Should().Be("AmountLimitRule");
    }
}
