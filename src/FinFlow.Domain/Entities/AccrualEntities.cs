using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>
/// Ежедневное начисление процентов по формуле (1.5).
/// Уникальный индекс (ContractId, AccrualDate) обеспечивает идемпотентность закрытия дня.
/// </summary>
public class InterestAccrual : BaseEntity
{
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; }
    public DateOnly AccrualDate { get; set; }
    public decimal Base { get; set; }
    public decimal RatePercent { get; set; }
    public int YearDays { get; set; }
    public decimal Amount { get; set; }
    public AccrualStatus Status { get; set; } = AccrualStatus.Accrued;
    public Guid? OperationalDayId { get; set; }
}

/// <summary>Удержанный подоходный налог по формуле (1.8).</summary>
public class TaxWithholding : BaseEntity
{
    public Guid AccrualId { get; set; }
    public InterestAccrual Accrual { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; }
    public decimal TaxBase { get; set; }
    public decimal RatePercent { get; set; }
    public decimal Amount { get; set; }
    public DateTime WithheldAt { get; set; }
    public bool IsExempt { get; set; }
    public string ExemptionReason { get; set; }
}

/// <summary>Операционный день и этапы закрытия (п. 1.6.3, требование 12).</summary>
public class OperationalDay : BaseEntity
{
    public DateOnly Date { get; set; }
    public OperationalDayStatus Status { get; set; } = OperationalDayStatus.Open;
    public DayCloseStage Stage { get; set; } = DayCloseStage.Started;
    public DateTime? StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string LastError { get; set; }
    public int AccrualCount { get; set; }
    public int PostingCount { get; set; }
    public int RejectedOperations { get; set; }
    public decimal InterestAccrued { get; set; }
    public decimal TaxWithheld { get; set; }
    public bool IsClosed => Status == OperationalDayStatus.Closed;
}

/// <summary>Сохранённый прогноз оттока и разрыва ликвидности по срокам (таблица LiquidityForecasts).</summary>
public class LiquidityForecast : BaseEntity
{
    public Guid CalcId { get; set; }
    public DateTime CalcDate { get; set; }
    public DateOnly Bucket { get; set; }
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
    public decimal Cash { get; set; }
    public decimal MinimumCash { get; set; }
    public decimal Gap { get; set; }
    public string Scenario { get; set; }
}
