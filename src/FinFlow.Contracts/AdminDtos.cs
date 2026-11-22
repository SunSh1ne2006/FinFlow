namespace FinFlow.Contracts;

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
    public string Role { get; set; } = "Employee";
}
