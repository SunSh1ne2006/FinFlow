namespace FinFlow.Contracts;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; }
    public string KindText { get; set; }
    public string Currency { get; set; }
    public string BalanceAccount { get; set; }
    public bool IsActive { get; set; }
    public string Description { get; set; }
    public IReadOnlyList<ProductVersionDto> Versions { get; set; } = Array.Empty<ProductVersionDto>();
}

public class ProductVersionDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Version { get; set; }
    public decimal RatePercent { get; set; }
    public string RateType { get; set; }
    public decimal SpreadPercent { get; set; }
    public string DepositType { get; set; }
    public string DepositTypeText { get; set; }
    public bool IsRevocable { get; set; }
    public bool AllowsProlongation { get; set; }
    public int TermMonths { get; set; }
    public string Capitalization { get; set; }
    public string InterestPayout { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MinBalance { get; set; }
    public int DayBase { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public decimal? RvsrLimit { get; set; }
    public bool RequiresBankConsentForEarlyReturn { get; set; }
    public string Notes { get; set; }
}

public class CreateProductRequest
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; } = "Deposit";
    public string Currency { get; set; } = "BYN";
    public string BalanceAccount { get; set; }
    public string Description { get; set; }
}

public class CreateProductVersionRequest
{
    public decimal RatePercent { get; set; }
    public string RateType { get; set; } = "Fixed";
    public decimal SpreadPercent { get; set; }
    public string DepositType { get; set; } = "Term";
    public bool IsRevocable { get; set; } = true;
    public bool AllowsProlongation { get; set; }
    public int TermMonths { get; set; }
    public string Capitalization { get; set; } = "None";
    public string InterestPayout { get; set; } = "AtEnd";
    public decimal MinAmount { get; set; }
    public decimal MinBalance { get; set; }
    public int DayBase { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public string Notes { get; set; }
}

public class RvsrCheckDto
{
    public Guid VersionId { get; set; }
    public decimal RatePercent { get; set; }
    public decimal RvsrLimit { get; set; }
    public bool IsExceeded { get; set; }
    public string Message { get; set; }
}
