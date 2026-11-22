using FinFlow.Domain.Common;

namespace FinFlow.Domain.Calculators;

/// <summary>Показатели работы одного сотрудника за выбранный период.</summary>
public sealed class StaffWorkRecord
{
    public Guid UserId { get; init; }
    public string Login { get; init; }
    public string FullName { get; init; }
    public string Role { get; init; }
    public int ClientsRegistered { get; init; }
    public int AccountsOpened { get; init; }
    public int DepositsOpened { get; init; }
    public decimal DepositAmount { get; init; }
    public int OperationsCount { get; init; }
    public decimal OperationsAmount { get; init; }
    public int DayCloses { get; init; }
}

/// <summary>Итоги отчёта о работе сотрудников.</summary>
public sealed class StaffWorkTotals
{
    public int Employees { get; init; }
    public int ClientsRegistered { get; init; }
    public int AccountsOpened { get; init; }
    public int DepositsOpened { get; init; }
    public decimal DepositAmount { get; init; }
    public int OperationsCount { get; init; }
    public decimal OperationsAmount { get; init; }
    public int DayCloses { get; init; }
}

/// <summary>Свёртка отчёта о работе сотрудников: итоги по всем строкам.</summary>
public static class StaffWorkCalculator
{
    public static StaffWorkTotals Summarize(IEnumerable<StaffWorkRecord> records)
    {
        var rows = (records ?? Enumerable.Empty<StaffWorkRecord>())
            .Where(r => r != null)
            .ToList();

        return new StaffWorkTotals
        {
            Employees = rows.Count,
            ClientsRegistered = rows.Sum(r => r.ClientsRegistered),
            AccountsOpened = rows.Sum(r => r.AccountsOpened),
            DepositsOpened = rows.Sum(r => r.DepositsOpened),
            DepositAmount = Money.Round(rows.Sum(r => r.DepositAmount)),
            OperationsCount = rows.Sum(r => r.OperationsCount),
            OperationsAmount = Money.Round(rows.Sum(r => r.OperationsAmount)),
            DayCloses = rows.Sum(r => r.DayCloses)
        };
    }
}
