namespace FinFlow.Contracts;

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
    public int RejectedOperations { get; set; }
    public decimal InterestAccrued { get; set; }
    public decimal TaxWithheld { get; set; }
    public string LastError { get; set; }
}
