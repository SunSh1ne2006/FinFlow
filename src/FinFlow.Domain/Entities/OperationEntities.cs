using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>
/// Операция по счёту. Жизненный цикл соответствует таблице 1.11 записки:
/// Черновик → На проверке → (Отказано | Приостановлена | На контроле | Одобрена) →
/// Исполнена → Отражена → Сторнирована.
/// </summary>
public class Operation : BaseEntity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; }
    public Guid? ContractId { get; set; }
    public Contract Contract { get; set; }
    public Guid DocumentId { get; set; }
    public OperationDocument Document { get; set; }
    public OperationKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BYN";
    public OperationStatus Status { get; set; } = OperationStatus.Draft;
    public string Description { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string RejectionReason { get; set; }
    public Guid? ReversalOfId { get; set; }
    /// <summary>Ключ идемпотентности документа: один документ исполняется один раз.</summary>
    public string IdempotencyKey { get; set; }
    /// <summary>Отпечаток ЭЦП при подтверждении операции выше лимита (имитация внешнего криптопровайдера).</summary>
    public string Signature { get; set; }
    /// <summary>Снимок значений B, L, H и D на момент проверки — для объяснения отказа.</summary>
    public decimal CheckedBalance { get; set; }
    public decimal CheckedOverdraft { get; set; }
    public decimal CheckedHolds { get; set; }
    public decimal CheckedAvailable { get; set; }

    public List<Posting> Postings { get; set; } = new();

    public bool IsFinal => Status is OperationStatus.Rejected or OperationStatus.Reversed;

    /// <summary>Автор операции не может подтвердить её сам (бизнес-правило 12).</summary>
    public bool CanBeApprovedBy(Guid userId) =>
        Status == OperationStatus.OnControl && CreatedBy != userId;
}

/// <summary>Документ-основание или входящий документ внешней системы (требование 23).</summary>
public class OperationDocument : BaseEntity
{
    public string Number { get; set; }
    public DocumentSource SourceSystem { get; set; } = DocumentSource.Branch;
    public string ExternalId { get; set; }
    public string Kind { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string Payload { get; set; }
    public Guid? ClientId { get; set; }
}

/// <summary>Проводка для главной книги АБС. Изменение и удаление запрещены — только сторнирование.</summary>
public class Posting : BaseEntity
{
    /// <summary>Операция, породившая проводку; null для проводок начисления процентов и налога.</summary>
    public Guid? OperationId { get; set; }
    public Operation Operation { get; set; }
    public Guid? DayId { get; set; }
    public string DebitAccount { get; set; }
    public string CreditAccount { get; set; }
    public string Currency { get; set; } = "BYN";
    public decimal Amount { get; set; }
    public string Purpose { get; set; }
    public bool IsReversed { get; set; }
    public bool IsLedgerPosted { get; set; }
}
