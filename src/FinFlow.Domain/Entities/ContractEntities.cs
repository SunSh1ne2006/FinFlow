using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>
/// Договор вклада или текущего счёта. Ссылается на конкретную версию условий,
/// поэтому изменение продукта не затрагивает действующие договоры (бизнес-правило 2, 3).
/// </summary>
public class Contract : BaseEntity
{
    public string Number { get; set; }
    public Guid ClientId { get; set; }
    public Client Client { get; set; }
    public Guid AccountId { get; set; }
    public Account Account { get; set; }
    public Guid ProductVersionId { get; set; }
    public ProductVersion ProductVersion { get; set; }
    public DateOnly OpenDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Principal { get; set; }
    /// <summary>Начисленные, но ещё не выплаченные проценты I.</summary>
    public decimal AccruedInterest { get; set; }
    public decimal PaidInterest { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public int ProlongationCount { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? EarlyReturnConsentBy { get; set; }
    public DateTime? EarlyReturnConsentAt { get; set; }
    public string Notes { get; set; }

    public List<InterestAccrual> Accruals { get; set; } = new();
    public List<Operation> Operations { get; set; } = new();

    public bool IsActive => Status is ContractStatus.Active or ContractStatus.Restricted or ContractStatus.Prolonged;

    public int TermDays => EndDate.DayNumber - OpenDate.DayNumber;

    /// <summary>Согласие банка на досрочный возврат безотзывного вклада (бизнес-правило 8).</summary>
    public bool HasEarlyReturnConsent => EarlyReturnConsentBy != null;
}
