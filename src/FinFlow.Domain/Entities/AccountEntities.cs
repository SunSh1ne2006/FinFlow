using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>Счёт по Плану счетов бухгалтерского учёта в банках (постановление № 126).</summary>
public class Account : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; }
    public string BalanceAccount { get; set; }
    public string Currency { get; set; } = "BYN";
    /// <summary>Балансовый остаток B (допускает отрицательное значение при овердрафте).</summary>
    public decimal Balance { get; set; }
    /// <summary>Лимит овердрафта L.</summary>
    public decimal OverdraftLimit { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Open;
    public DateTime? ClosedAt { get; set; }

    public List<AccountNumber> Numbers { get; set; } = new();
    public List<Hold> Holds { get; set; } = new();
    public List<Operation> Operations { get; set; } = new();
    public List<Contract> Contracts { get; set; } = new();

    public bool IsOpen => Status == AccountStatus.Open;

    public string CurrentNumber(DateOnly date) =>
        Numbers.Where(n => n.ValidFrom <= date && (n.ValidTo == null || n.ValidTo >= date))
               .OrderByDescending(n => n.ValidFrom)
               .Select(n => n.Number)
               .FirstOrDefault() ?? Numbers.FirstOrDefault()?.Number;
}

/// <summary>
/// Прежний и изменённый номер счёта (постановление № 123, переход с 01.01.2027).
/// Поиск счёта выполняется по любому из номеров.
/// </summary>
public class AccountNumber : BaseEntity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; }
    public string Number { get; set; }
    public NumberKind Kind { get; set; } = NumberKind.Previous;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}

/// <summary>
/// Ограничение: арест, приостановление операций, бронирование, авторизационная блокировка (требование 6).
/// Сумма действующих ограничений образует H в формуле (1.1).
/// </summary>
public class Hold : BaseEntity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; }
    public HoldSource Source { get; set; }
    public string BasisNumber { get; set; }
    public string BasisAuthority { get; set; }
    /// <summary>Признак ареста или приостановления: при нём лимит овердрафта не применяется.</summary>
    public bool IsArrest { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public HoldStatus Status { get; set; } = HoldStatus.Active;
    public DateTime? ReleasedAt { get; set; }
    public string Comment { get; set; }

    public bool IsActiveOn(DateOnly date) =>
        Status == HoldStatus.Active
        && ValidFrom <= date
        && (ValidTo == null || ValidTo >= date);
}
