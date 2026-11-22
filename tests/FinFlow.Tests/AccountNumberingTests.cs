using FinFlow.Domain.Calculators;
using FinFlow.Domain.Enums;
using Xunit;

namespace FinFlow.Tests;

/// <summary>Нумерация счетов (постановление НБРБ № 123) и расчёт срока договора вклада.</summary>
public class AccountNumberingTests
{
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
