namespace FinFlow.Contracts;

public class ContractDto
{
    public Guid Id { get; set; }
    public string Number { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; }
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public Guid ProductVersionId { get; set; }
    public string ProductName { get; set; }
    public DateOnly OpenDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Principal { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal PaidInterest { get; set; }
    public string Status { get; set; }
    public string StatusText { get; set; }
    public decimal RatePercent { get; set; }
    public string DepositType { get; set; }
    public bool IsRevocable { get; set; }
    public decimal MinBalance { get; set; }
    public int TermMonths { get; set; }
    public int ProlongationCount { get; set; }
    public int TermDays { get; set; }
    public bool HasEarlyReturnConsent { get; set; }
    public string Currency { get; set; }
}

public class OpenContractRequest
{
    public Guid ClientId { get; set; }
    public Guid ProductVersionId { get; set; }
    public decimal Amount { get; set; }
    public int? TermMonths { get; set; }
    public string Notes { get; set; }
}

public class EarlyReturnQuoteDto
{
    public Guid ContractId { get; set; }
    public decimal Principal { get; set; }
    public int Days { get; set; }
    public int YearDays { get; set; }
    public decimal ContractRatePercent { get; set; }
    public decimal DemandRatePercent { get; set; }
    public decimal ContractInterest { get; set; }
    public decimal DemandInterest { get; set; }
    public decimal PaidInterest { get; set; }
    public decimal ExcessToWithhold { get; set; }
    public decimal NetPayout { get; set; }
    public bool RequiresBankConsent { get; set; }
    public bool ConsentGranted { get; set; }
    public bool Allowed { get; set; }
    public string Reason { get; set; }
}

public class InterestAccrualDto
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public DateOnly AccrualDate { get; set; }
    public decimal Base { get; set; }
    public decimal RatePercent { get; set; }
    public int YearDays { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
}
