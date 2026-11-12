namespace FinFlow.Contracts;

public class OperationDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public string ClientName { get; set; }
    public Guid? ContractId { get; set; }
    public string ContractNumber { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; }
    public string Kind { get; set; }
    public string KindText { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Status { get; set; }
    public string StatusText { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public string CreatedByLogin { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string ApprovedByLogin { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string RejectionReason { get; set; }
    public string Signature { get; set; }
    public decimal CheckedBalance { get; set; }
    public decimal CheckedOverdraft { get; set; }
    public decimal CheckedHolds { get; set; }
    public decimal CheckedAvailable { get; set; }
    public bool CanBeApproved { get; set; }
}

public class CreateOperationRequest
{
    public Guid AccountId { get; set; }
    public Guid? ContractId { get; set; }
    public string Kind { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; }
    public string DocumentNumber { get; set; }
    public string SourceSystem { get; set; } = "Branch";
    /// <summary>Идентификатор документа во внешней системе: обеспечивает идемпотентность приёма.</summary>
    public string ExternalId { get; set; }
}

public class ApproveRequest
{
    public string Signature { get; set; }
}

public class RejectRequest
{
    public string Reason { get; set; }
}

public class ReverseRequest
{
    public string Reason { get; set; }
}

public class AmlDecisionRequest
{
    public bool Approve { get; set; }
    public string Reason { get; set; }
}

public class BalanceSnapshotDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public decimal Balance { get; set; }
    public decimal OverdraftLimit { get; set; }
    public decimal HoldsAmount { get; set; }
    public bool HasArrest { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal OverdraftUsed { get; set; }
    public decimal OverdraftUtilizationPercent { get; set; }
    public decimal MinBalance { get; set; }
    public DateOnly Date { get; set; }
}
