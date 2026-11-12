namespace FinFlow.Contracts;

public class ParameterDto
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal Value { get; set; }
    public string TextValue { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; }
}

public class SetParameterRequest
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal Value { get; set; }
    public string TextValue { get; set; }
    public DateOnly? ValidFrom { get; set; }
}

public class AuditRecordDto
{
    public Guid Id { get; set; }
    public DateTime At { get; set; }
    public string UserLogin { get; set; }
    public string Action { get; set; }
    public string EntityName { get; set; }
    public string EntityId { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
}

public class CreateUserRequest
{
    public string Login { get; set; }
    public string FullName { get; set; }
    public string Password { get; set; }
    public string Role { get; set; } = "Operator";
    public bool TwoFactorEnabled { get; set; }
}

public class AisIdoDocumentRequest
{
    public string ExternalId { get; set; }
    /// <summary>Arrest, Suspension или PaymentRequest.</summary>
    public string Kind { get; set; } = "Arrest";
    public string AccountNumber { get; set; }
    public decimal Amount { get; set; }
    public string BasisNumber { get; set; }
    public string BasisAuthority { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string Comment { get; set; }
}

public class IntegrationResultDto
{
    public bool Accepted { get; set; }
    public bool Duplicate { get; set; }
    public string Message { get; set; }
    public Guid? HoldId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? DocumentId { get; set; }
}
