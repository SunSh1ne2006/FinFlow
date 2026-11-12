namespace FinFlow.Domain.Enums;

public enum ClientType
{
    Individual = 1,
    LegalEntity = 2,
    Entrepreneur = 3
}

public enum IdentificationStatus
{
    NotIdentified = 0,
    Identified = 1,
    RequiresUpdate = 2
}

public enum KycRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum ProductKind
{
    Deposit = 1,
    CurrentAccount = 2
}

/// <summary>Виды вкладов по Декрету Президента Республики Беларусь № 7.</summary>
public enum DepositType
{
    None = 0,
    Demand = 1,
    Term = 2,
    Conditional = 3
}

public enum RateType
{
    Fixed = 1,
    Floating = 2
}

public enum CapitalizationMode
{
    None = 1,
    Monthly = 2
}

public enum InterestPayoutMode
{
    AtEnd = 1,
    Monthly = 2,
    OnDemand = 3
}

public enum AccountStatus
{
    Open = 1,
    Closed = 2
}

public enum NumberKind
{
    Previous = 1,
    Modified = 2
}

public enum ContractStatus
{
    Draft = 0,
    Active = 1,
    Restricted = 2,
    Prolonged = 3,
    EarlyReturned = 4,
    Closed = 5
}

public enum HoldSource
{
    AisIdo = 1,
    Bank = 2,
    CardAuthorization = 3,
    Booking = 4
}

public enum HoldStatus
{
    Active = 1,
    Released = 2,
    Expired = 3
}

public enum OperationKind
{
    CashIn = 1,
    TransferIn = 2,
    CashOut = 3,
    TransferOut = 4,
    PartialWithdrawal = 5,
    EarlyReturn = 6,
    InterestPayment = 7,
    Capitalization = 8,
    TaxWithholding = 9,
    Reversal = 10
}

public enum OperationStatus
{
    Draft = 0,
    OnCheck = 1,
    Rejected = 2,
    Suspended = 3,
    OnControl = 4,
    Approved = 5,
    Executed = 6,
    Posted = 7,
    Reversed = 8
}

public enum DocumentSource
{
    Branch = 1,
    Dbo = 2,
    AisIdo = 3,
    System = 4
}

public enum AccrualStatus
{
    Accrued = 1,
    Capitalized = 2,
    Paid = 3
}

public enum OperationalDayStatus
{
    Open = 1,
    Closing = 2,
    Closed = 3,
    Failed = 4
}

public enum DayCloseStage
{
    Started = 1,
    PendingRejected = 2,
    AccrualsDone = 3,
    PayoutsDone = 4,
    ContractsDone = 5,
    PostingsDone = 6,
    Reconciled = 7,
    Completed = 8
}

public enum RiskStatus
{
    Active = 1,
    Mitigated = 2,
    Closed = 3
}

public enum RiskZone
{
    Low = 1,
    Moderate = 2,
    High = 3,
    Critical = 4
}

public enum RoleCode
{
    Operator = 1,
    Controller = 2,
    Accountant = 3,
    Administrator = 4,
    Head = 5,
    Treasurer = 6,
    RiskManager = 7,
    AmlOfficer = 8
}

/// <summary>Направление операции: поступление увеличивает остаток, списание — уменьшает.</summary>
public static class OperationKindExtensions
{
    public static bool IsDebit(this OperationKind kind) => kind switch
    {
        OperationKind.CashOut => true,
        OperationKind.TransferOut => true,
        OperationKind.PartialWithdrawal => true,
        OperationKind.EarlyReturn => true,
        OperationKind.TaxWithholding => true,
        OperationKind.Reversal => true,
        _ => false
    };

    public static bool IsSystem(this OperationKind kind) =>
        kind is OperationKind.InterestPayment or OperationKind.Capitalization or OperationKind.TaxWithholding;

    public static bool IsCash(this OperationKind kind) =>
        kind is OperationKind.CashIn or OperationKind.CashOut;

    public static string DisplayName(this OperationKind kind) => kind switch
    {
        OperationKind.CashIn => "Взнос наличными",
        OperationKind.TransferIn => "Безналичное поступление",
        OperationKind.CashOut => "Выдача наличными",
        OperationKind.TransferOut => "Безналичный перевод",
        OperationKind.PartialWithdrawal => "Частичное снятие",
        OperationKind.EarlyReturn => "Досрочный возврат вклада",
        OperationKind.InterestPayment => "Выплата процентов",
        OperationKind.Capitalization => "Капитализация процентов",
        OperationKind.TaxWithholding => "Удержание подоходного налога",
        OperationKind.Reversal => "Сторно операции",
        _ => kind.ToString()
    };

    public static string DisplayName(this OperationStatus status) => status switch
    {
        OperationStatus.Draft => "Черновик",
        OperationStatus.OnCheck => "На проверке",
        OperationStatus.Rejected => "Отказано",
        OperationStatus.Suspended => "Приостановлена",
        OperationStatus.OnControl => "На контроле",
        OperationStatus.Approved => "Одобрена",
        OperationStatus.Executed => "Исполнена",
        OperationStatus.Posted => "Отражена",
        OperationStatus.Reversed => "Сторнирована",
        _ => status.ToString()
    };

    public static string DisplayName(this ContractStatus status) => status switch
    {
        ContractStatus.Draft => "Оформлен",
        ContractStatus.Active => "Действует",
        ContractStatus.Restricted => "Ограничен",
        ContractStatus.Prolonged => "Пролонгирован",
        ContractStatus.EarlyReturned => "Досрочно возвращён",
        ContractStatus.Closed => "Закрыт",
        _ => status.ToString()
    };

    public static string DisplayName(this RiskZone zone) => zone switch
    {
        RiskZone.Low => "Низкий",
        RiskZone.Moderate => "Умеренный",
        RiskZone.High => "Высокий",
        RiskZone.Critical => "Критический",
        _ => zone.ToString()
    };
}
