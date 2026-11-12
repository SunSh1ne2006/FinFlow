namespace FinFlow.Contracts;

public class LiquiditySettingsDto
{
    public decimal OpeningCash { get; set; }
    public decimal MinimumCash { get; set; }
    public decimal RevocablePortfolio { get; set; }
    public decimal ProlongationShare { get; set; }
    public decimal EarlyWithdrawalShare { get; set; }
    public decimal ExpectedDailyInflow { get; set; }
    public string Unit { get; set; } = "р.";
}

public class LiquidityDayDto
{
    public DateOnly Date { get; set; }
    public decimal MaturingPrincipal { get; set; }
    public decimal ProlongedPrincipal { get; set; }
    public decimal EarlyWithdrawal { get; set; }
    public decimal InterestPayable { get; set; }
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
    public decimal Cash { get; set; }
    public decimal Gap { get; set; }
}

public class LiquidityForecastDto
{
    public string Scenario { get; set; }
    public LiquiditySettingsDto Settings { get; set; }
    public IReadOnlyList<LiquidityDayDto> Days { get; set; } = Array.Empty<LiquidityDayDto>();
    public decimal MaxGap { get; set; }
    public decimal MinCash { get; set; }
    public int GapDays { get; set; }
    public DateOnly? FirstGapDate { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class MarginSegmentDto
{
    public string Name { get; set; }
    public decimal Volume { get; set; }
    public decimal RatePercent { get; set; }
    public decimal InterestExpense { get; set; }
}

public class MarginReportDto
{
    public string Scenario { get; set; }
    public string Unit { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal WeightedRate { get; set; }
    public decimal AnnualInterestExpense { get; set; }
    public decimal TransferRate { get; set; }
    public decimal CostPercent { get; set; }
    public decimal Margin { get; set; }
    public decimal RiskReserve { get; set; }
    public decimal NetMargin { get; set; }
    public IReadOnlyList<MarginSegmentDto> Segments { get; set; } = Array.Empty<MarginSegmentDto>();
    public DateTime CalculatedAt { get; set; }
}

public class RiskDto
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Event { get; set; }
    public string Cause { get; set; }
    public string Consequence { get; set; }
    public string Category { get; set; }
    public string OwnerRole { get; set; }
    public string Status { get; set; }
    public string Mitigation { get; set; }
    public string EarlyIndicator { get; set; }
    public decimal Probability { get; set; }
    public decimal MoneyImpact { get; set; }
    public decimal? MitigatedProbability { get; set; }
    public int ProbabilityScore { get; set; }
    public int ImpactScore { get; set; }
    public int Score { get; set; }
    public string Zone { get; set; }
    public string ZoneText { get; set; }
    public decimal Emv { get; set; }
    public decimal? ResidualEmv { get; set; }
    public DateOnly? AssessmentDate { get; set; }
}

public class RiskMatrixCellDto
{
    public int ProbabilityScore { get; set; }
    public int ImpactScore { get; set; }
    public int Score { get; set; }
    public string Zone { get; set; }
    public string ZoneText { get; set; }
    public IReadOnlyList<string> RiskCodes { get; set; } = Array.Empty<string>();
}

public class RiskSummaryDto
{
    public decimal ExpectedMonetaryValue { get; set; }
    public decimal StandardDeviation { get; set; }
    public decimal Reserve { get; set; }
    public decimal Z { get; set; }
    public decimal ResidualEmv { get; set; }
    public decimal MitigationEffect { get; set; }
    public IReadOnlyList<RiskDto> Risks { get; set; } = Array.Empty<RiskDto>();
    public IReadOnlyList<RiskMatrixCellDto> Matrix { get; set; } = Array.Empty<RiskMatrixCellDto>();
    public IReadOnlyList<SensitivityDto> Sensitivity { get; set; } = Array.Empty<SensitivityDto>();
    public DateTime CalculatedAt { get; set; }
}

public class SensitivityDto
{
    public string Factor { get; set; }
    public string BaseValue { get; set; }
    public string Change { get; set; }
    public string Impact { get; set; }
    public string Indicator { get; set; }
    public decimal ImpactValue { get; set; }
}

public class AssessRiskRequest
{
    public decimal Probability { get; set; }
    public decimal MoneyImpact { get; set; }
    public decimal? MitigatedProbability { get; set; }
    public string Notes { get; set; }
}

public class OperationalDayDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Status { get; set; }
    public string Stage { get; set; }
    public string StageText { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int AccrualCount { get; set; }
    public int PostingCount { get; set; }
    public int RejectedOperations { get; set; }
    public decimal InterestAccrued { get; set; }
    public decimal TaxWithheld { get; set; }
    public string LastError { get; set; }
}

public class ReconciliationDto
{
    public DateOnly Date { get; set; }
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
    public decimal Difference { get; set; }
    public int PostingCount { get; set; }
    public decimal AccrualTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public bool IsBalanced { get; set; }
    public string Message { get; set; }
}

public class TaxReportRowDto
{
    public string ContractNumber { get; set; }
    public string ClientName { get; set; }
    public string Currency { get; set; }
    public decimal Accrued { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal Tax { get; set; }
    public bool IsExempt { get; set; }
    public string ExemptionReason { get; set; }
}

public class TaxReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal TotalAccrued { get; set; }
    public decimal TotalTaxable { get; set; }
    public decimal TotalTax { get; set; }
    public IReadOnlyList<TaxReportRowDto> Rows { get; set; } = Array.Empty<TaxReportRowDto>();
}

public class PortfolioSegmentReportDto
{
    public string Name { get; set; }
    public string Currency { get; set; }
    public decimal Volume { get; set; }
    public decimal RatePercent { get; set; }
    public int Contracts { get; set; }
}

public class PortfolioReportDto
{
    public DateOnly AsOf { get; set; }
    public int TotalContracts { get; set; }
    public decimal TotalPrincipal { get; set; }
    public decimal TotalAccruedInterest { get; set; }
    public IReadOnlyList<PortfolioSegmentReportDto> Segments { get; set; } = Array.Empty<PortfolioSegmentReportDto>();
}

public class AgvRegisterRowDto
{
    public string ClientName { get; set; }
    public string IdNumber { get; set; }
    public string ContractNumber { get; set; }
    public string Currency { get; set; }
    public decimal Amount { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal Total { get; set; }
}

public class AgvRegisterDto
{
    public DateOnly AsOf { get; set; }
    public int ClientsCount { get; set; }
    public decimal TotalAmount { get; set; }
    public IReadOnlyList<AgvRegisterRowDto> Rows { get; set; } = Array.Empty<AgvRegisterRowDto>();
}

public class AmlQueueItemDto
{
    public OperationDto Operation { get; set; }
    public string ClientRiskLevel { get; set; }
    public string KycStatus { get; set; }
    public string SuspicionReason { get; set; }
}
