namespace FinFlow.Contracts;

public class ClientListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string TypeText { get; set; }
    public string IdNumber { get; set; }
    public string IdentificationStatus { get; set; }
    public bool IsIdentified { get; set; }
    public int AccountsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientDetailsDto : ClientListItemDto
{
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
    public bool IsResident { get; set; }
    public DateOnly? BirthDate { get; set; }
    public DateTime? IdentifiedAt { get; set; }
    public string Notes { get; set; }
    public IReadOnlyList<AccountListItemDto> Accounts { get; set; } = Array.Empty<AccountListItemDto>();
}

public class CreateClientRequest
{
    public string Type { get; set; } = "Individual";
    public string Name { get; set; }
    public string IdNumber { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Country { get; set; } = "BY";
    public bool IsResident { get; set; } = true;
    public string Notes { get; set; }
}

public class UpdateClientRequest
{
    public string Name { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Notes { get; set; }
    public bool? Identify { get; set; }
}
