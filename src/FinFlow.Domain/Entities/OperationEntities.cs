using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>
/// Операция по счёту: создаётся сотрудником и исполняется сразу после проверки доступного остатка.
/// Исполненная операция не изменяется — для исправления применяется сторнирование.
/// </summary>
public class Operation : BaseEntity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; }
    public Guid? ContractId { get; set; }
    public Contract Contract { get; set; }
    /// <summary>Номер документа-основания (кассовый ордер, платёжное поручение).</summary>
    public string DocumentNumber { get; set; }
    public OperationKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BYN";
    public OperationStatus Status { get; set; } = OperationStatus.Draft;
    public string Description { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string RejectionReason { get; set; }
    public Guid? ReversalOfId { get; set; }
    /// <summary>Снимок значений B, L, H и D на момент проверки — для объяснения отказа.</summary>
    public decimal CheckedBalance { get; set; }
    public decimal CheckedOverdraft { get; set; }
    public decimal CheckedHolds { get; set; }
    public decimal CheckedAvailable { get; set; }

    public bool IsFinal => Status is OperationStatus.Rejected or OperationStatus.Reversed;
}
