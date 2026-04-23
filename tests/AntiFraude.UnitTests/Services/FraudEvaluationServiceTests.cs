using AntiFraude.Application.Services;
using AntiFraude.Domain.Entities;
using AntiFraude.Domain.Interfaces;
using AntiFraude.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AntiFraude.UnitTests.Services;

public class FraudEvaluationServiceTests
{
    private static Transaction CreateTransaction(decimal amount = 100m)
        => Transaction.Create("TXN-001", amount, "MRC-001", "CUS-001", "BRL");

    [Fact]
    public async Task EvaluateAsync_WhenAllRulesPass_ShouldReturnApproved()
    {
        // Arrange
        var rule1 = new Mock<IFraudRule>();
        rule1.Setup(r => r.RuleName).Returns("Rule1");
        rule1.Setup(r => r.EvaluateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(FraudRuleResult.Approved());

        var rule2 = new Mock<IFraudRule>();
        rule2.Setup(r => r.RuleName).Returns("Rule2");
        rule2.Setup(r => r.EvaluateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(FraudRuleResult.Approved());

        var sut = new FraudEvaluationService(
            [rule1.Object, rule2.Object],
            NullLogger<FraudEvaluationService>.Instance);

        // Act
        var (decision, reason) = await sut.EvaluateAsync(CreateTransaction());

        // Assert
        decision.Should().Be(Domain.Enums.TransactionDecision.APPROVED);
        reason.Should().BeNull();
        rule1.Verify(r => r.EvaluateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Once);
        rule2.Verify(r => r.EvaluateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_WhenFirstRuleRejects_ShouldReturnRejectedAndSkipSubsequentRules()
    {
        // Arrange — fail-fast: segunda regra NÃO deve ser executada
        var rule1 = new Mock<IFraudRule>();
        rule1.Setup(r => r.RuleName).Returns("RejectingRule");
        rule1.Setup(r => r.EvaluateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(FraudRuleResult.Rejected("AMOUNT_EXCEEDS_LIMIT"));

        var rule2 = new Mock<IFraudRule>();
        rule2.Setup(r => r.RuleName).Returns("NeverReachedRule");

        var sut = new FraudEvaluationService(
            [rule1.Object, rule2.Object],
            NullLogger<FraudEvaluationService>.Instance);

        // Act
        var (decision, reason) = await sut.EvaluateAsync(CreateTransaction(15_000m));

        // Assert
        decision.Should().Be(Domain.Enums.TransactionDecision.REJECTED);
        reason.Should().Be("AMOUNT_EXCEEDS_LIMIT");
        rule2.Verify(r => r.EvaluateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoRules_ShouldReturnApproved()
    {
        // Arrange — sem regras registradas, aprovação por padrão
        var sut = new FraudEvaluationService(
            [],
            NullLogger<FraudEvaluationService>.Instance);

        // Act
        var (decision, reason) = await sut.EvaluateAsync(CreateTransaction());

        // Assert
        decision.Should().Be(Domain.Enums.TransactionDecision.APPROVED);
        reason.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WhenSecondRuleRejects_ShouldReturnRejectedWithCorrectReason()
    {
        // Arrange
        var rule1 = new Mock<IFraudRule>();
        rule1.Setup(r => r.RuleName).Returns("PassingRule");
        rule1.Setup(r => r.EvaluateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(FraudRuleResult.Approved());

        var rule2 = new Mock<IFraudRule>();
        rule2.Setup(r => r.RuleName).Returns("HighRiskRule");
        rule2.Setup(r => r.EvaluateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(FraudRuleResult.Rejected("HIGH_RISK_CUSTOMER"));

        var sut = new FraudEvaluationService(
            [rule1.Object, rule2.Object],
            NullLogger<FraudEvaluationService>.Instance);

        // Act
        var (decision, reason) = await sut.EvaluateAsync(CreateTransaction());

        // Assert
        decision.Should().Be(Domain.Enums.TransactionDecision.REJECTED);
        reason.Should().Be("HIGH_RISK_CUSTOMER");
    }
}
