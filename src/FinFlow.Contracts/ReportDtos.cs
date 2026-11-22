namespace FinFlow.Contracts;

/// <summary>Строка отчёта о работе сотрудника за период.</summary>
public class StaffReportRowDto
{
    public Guid UserId { get; set; }
    public string Login { get; set; }
    public string FullName { get; set; }
    public string Role { get; set; }
    public int ClientsRegistered { get; set; }
    public int AccountsOpened { get; set; }
    public int DepositsOpened { get; set; }
    public decimal DepositAmount { get; set; }
    public int OperationsCount { get; set; }
    public decimal OperationsAmount { get; set; }
    public int DayCloses { get; set; }
}

/// <summary>Отчёт руководителя о работе сотрудников за выбранный период.</summary>
public class StaffReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateTime GeneratedAt { get; set; }
    public StaffReportTotalsDto Totals { get; set; } = new();
    public IReadOnlyList<StaffReportRowDto> Rows { get; set; } = Array.Empty<StaffReportRowDto>();
}

public class StaffReportTotalsDto
{
    public int Employees { get; set; }
    public int ClientsRegistered { get; set; }
    public int AccountsOpened { get; set; }
    public int DepositsOpened { get; set; }
    public decimal DepositAmount { get; set; }
    public int OperationsCount { get; set; }
    public decimal OperationsAmount { get; set; }
    public int DayCloses { get; set; }
}
