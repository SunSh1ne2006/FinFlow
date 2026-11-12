using FinFlow.Domain.Calculators;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Xunit;

namespace FinFlow.Tests;

/// <summary>Маршрутизация операций (бизнес-правило 12) и нумерация счетов (НБРБ № 123).</summary>
public class WorkflowTests
{
    private static WorkflowLimits Limits() => new()
    {
        OperatorLimit = ModelBankData.OperatorLimit,
        AmlThreshold = ModelBankData.AmlThreshold
    };

    [Theory]
    [InlineData(3000, false, OperationRoute.AutoApprove)]
    [InlineData(5000, false, OperationRoute.AutoApprove)]
    [InlineData(5001, false, OperationRoute.Control)]
    [InlineData(19999, false, OperationRoute.Control)]
    [InlineData(20000, true, OperationRoute.Suspend)]
    [InlineData(15000, true, OperationRoute.Suspend)]
    public void Operation_route_matches_limits(decimal amount, bool amlSigns, OperationRoute expected)
    {
        Assert.Equal(expected, WorkflowPolicy.Evaluate(amount, Limits(), amlSigns));
    }

    [Theory]
    [InlineData(20000, false, true)]
    [InlineData(19999, false, false)]
    [InlineData(1, true, true)]
    public void Aml_signs_are_detected_by_threshold_or_client_risk(decimal amount, bool highRisk, bool expected)
    {
        Assert.Equal(expected, WorkflowPolicy.HasAmlSigns(amount, ModelBankData.AmlThreshold, highRisk));
    }

    /// <summary>До 01.01.2027 действует 13-значный номер счёта, с 2027 — 17-значный.</summary>
    [Fact]
    public void Account_number_length_depends_on_period()
    {
        var clientId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var previous = AccountNumberGenerator.Build("3014", clientId, 1, NumberKind.Previous);
        var modified = AccountNumberGenerator.Build("3014", clientId, 1, NumberKind.Modified);

        Assert.Equal(AccountNumberGenerator.PreviousLength, previous.Length);
        Assert.Equal(AccountNumberGenerator.ModifiedLength, modified.Length);
        Assert.Equal(new DateOnly(2027, 1, 1), AccountNumberGenerator.ModifiedNumbersStart);
        Assert.StartsWith("3014", previous);
        Assert.StartsWith("3014", modified);
    }

    /// <summary>Срок договора: 31 января + 1 месяц = 28 февраля (не 31).</summary>
    [Fact]
    public void Month_addition_is_clamped_to_month_length()
    {
        Assert.Equal(new DateOnly(2026, 2, 28), SchedulePolicy.AddMonthsClamped(new DateOnly(2026, 1, 31), 1));
        Assert.Equal(new DateOnly(2027, 2, 28), SchedulePolicy.AddMonthsClamped(new DateOnly(2026, 11, 30), 3));
    }
}
