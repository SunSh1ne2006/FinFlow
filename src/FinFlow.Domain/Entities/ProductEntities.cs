using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>Продукт: вклад или текущий счёт. Условия хранятся версиями (требование 2).</summary>
public class Product : BaseEntity
{
    public string Code { get; set; }
    public string Name { get; set; }
    public ProductKind Kind { get; set; }
    public string Currency { get; set; } = "BYN";
    public string BalanceAccount { get; set; }
    public bool IsActive { get; set; } = true;
    public string Description { get; set; }

    public List<ProductVersion> Versions { get; set; } = new();

    public ProductVersion CurrentVersion(DateOnly date) =>
        Versions.Where(v => v.IsApproved && v.ValidFrom <= date)
                .OrderByDescending(v => v.ValidFrom)
                .ThenByDescending(v => v.Version)
                .FirstOrDefault();
}

/// <summary>
/// Версия условий продукта. Неизменяема после утверждения: изменение создаёт новую версию,
/// договоры сохраняют свою версию (бизнес-правила 2 и 3).
/// </summary>
public class ProductVersion : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; }
    public int Version { get; set; } = 1;
    public decimal RatePercent { get; set; }
    public RateType RateType { get; set; } = RateType.Fixed;
    /// <summary>Надбавка к ставке рефинансирования для плавающей ставки, п. п.</summary>
    public decimal SpreadPercent { get; set; }
    public DepositType DepositType { get; set; } = DepositType.None;
    public bool IsRevocable { get; set; } = true;
    public bool AllowsProlongation { get; set; }
    public int TermMonths { get; set; }
    public CapitalizationMode Capitalization { get; set; } = CapitalizationMode.None;
    public InterestPayoutMode InterestPayout { get; set; } = InterestPayoutMode.AtEnd;
    public decimal MinAmount { get; set; }
    public decimal MinBalance { get; set; }
    /// <summary>База начисления: 365, 366 или 0 – фактическое число дней в году.</summary>
    public int DayBase { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsApproved { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    /// <summary>РВСР на дату утверждения: превышение требует отдельного решения КУАП.</summary>
    public decimal? RvsrLimit { get; set; }
    public string Notes { get; set; }

    /// <summary>Для безотзывного вклада досрочный возврат возможен только с согласия банка.</summary>
    public bool RequiresBankConsentForEarlyReturn =>
        DepositType != Enums.DepositType.None && DepositType != Enums.DepositType.Demand && !IsRevocable;

    public int ResolveDayBase(DateOnly date) => DayBase > 0 ? DayBase : (DateTime.IsLeapYear(date.Year) ? 366 : 365);
}
