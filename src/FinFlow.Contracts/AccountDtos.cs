namespace FinFlow.Contracts;

public class AccountListItemDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; }
    public string Number { get; set; }
    public string PreviousNumber { get; set; }
    public string ModifiedNumber { get; set; }
    public string Currency { get; set; }
    public string BalanceAccount { get; set; }
    public decimal Balance { get; set; }
    public decimal OverdraftLimit { get; set; }
    public string Status { get; set; }
    public string ContractNumber { get; set; }
    public string ProductName { get; set; }
    public decimal AvailableBalance { get; set; }
}

public class AccountDetailsDto : AccountListItemDto
{
    public decimal HoldsAmount { get; set; }
    public bool HasArrest { get; set; }
    public decimal AvailableBalanceCalculated { get; set; }
    public decimal OverdraftUsed { get; set; }
    public decimal OverdraftUtilizationPercent { get; set; }
    public IReadOnlyList<AccountNumberDto> Numbers { get; set; } = Array.Empty<AccountNumberDto>();
    public IReadOnlyList<HoldDto> Holds { get; set; } = Array.Empty<HoldDto>();
    public ContractDto Contract { get; set; }
    public IReadOnlyList<OperationDto> RecentOperations { get; set; } = Array.Empty<OperationDto>();
}

public class AccountNumberDto
{
    public Guid Id { get; set; }
    public string Number { get; set; }
    public string Kind { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}

public class HoldDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Source { get; set; }
    public string SourceText { get; set; }
    public string BasisNumber { get; set; }
    public string BasisAuthority { get; set; }
    public decimal Amount { get; set; }
    public bool IsArrest { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string Status { get; set; }
    public string Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateHoldRequest
{
    public string Source { get; set; } = "AisIdo";
    public string BasisNumber { get; set; }
    public string BasisAuthority { get; set; }
    public decimal Amount { get; set; }
    public bool IsArrest { get; set; } = true;
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string Comment { get; set; }
}

public class OpenAccountRequest
{
    public Guid ClientId { get; set; }
    public Guid ProductId { get; set; }
    public string Currency { get; set; }
    public decimal OverdraftLimit { get; set; }
    public decimal InitialAmount { get; set; }
}

public class SetOverdraftRequest
{
    public decimal OverdraftLimit { get; set; }
}
