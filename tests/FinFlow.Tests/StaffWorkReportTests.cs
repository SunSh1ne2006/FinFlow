using FinFlow.Domain.Calculators;
using Xunit;

namespace FinFlow.Tests;

/// <summary>Свёртка отчёта руководителя о работе сотрудников.</summary>
public class StaffWorkReportTests
{
    [Fact]
    public void Totals_are_summed_over_employees()
    {
        var records = new[]
        {
            new StaffWorkRecord
            {
                Login = "employee1",
                ClientsRegistered = 3,
                AccountsOpened = 4,
                DepositsOpened = 2,
                DepositAmount = 45000m,
                OperationsCount = 5,
                OperationsAmount = 12000m,
                DayCloses = 1
            },
            new StaffWorkRecord
            {
                Login = "employee2",
                ClientsRegistered = 2,
                AccountsOpened = 3,
                DepositsOpened = 1,
                DepositAmount = 20000m,
                OperationsCount = 4,
                OperationsAmount = 8000m,
                DayCloses = 2
            }
        };

        var totals = StaffWorkCalculator.Summarize(records);

        Assert.Equal(2, totals.Employees);
        Assert.Equal(5, totals.ClientsRegistered);
        Assert.Equal(7, totals.AccountsOpened);
        Assert.Equal(3, totals.DepositsOpened);
        Assert.Equal(65000m, totals.DepositAmount);
        Assert.Equal(9, totals.OperationsCount);
        Assert.Equal(20000m, totals.OperationsAmount);
        Assert.Equal(3, totals.DayCloses);
    }

    [Fact]
    public void Empty_report_has_zero_totals()
    {
        var totals = StaffWorkCalculator.Summarize(Array.Empty<StaffWorkRecord>());

        Assert.Equal(0, totals.Employees);
        Assert.Equal(0m, totals.DepositAmount);
        Assert.Equal(0m, totals.OperationsAmount);
    }
}
