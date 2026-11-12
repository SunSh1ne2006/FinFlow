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
    public string KycRiskLevel { get; set; }
    public int AccountsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientDetailsDto : ClientListItemDto
{
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
    public bool IsResident { get; set; }
    public DateTime? IdentifiedAt { get; set; }
    public string Notes { get; set; }
    public KycProfileDto Kyc { get; set; }
    public IReadOnlyList<RepresentativeDto> Representatives { get; set; } = Array.Empty<RepresentativeDto>();
    public IReadOnlyList<AccountListItemDto> Accounts { get; set; } = Array.Empty<AccountListItemDto>();
}

public class KycProfileDto
{
    public Guid? Id { get; set; }
    public string RiskLevel { get; set; }
    public DateOnly ReviewDate { get; set; }
    public string Source { get; set; }
    public string Notes { get; set; }
    public bool IsActual { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class RepresentativeDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; }
    public string IdNumber { get; set; }
    public string Authority { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public bool IsValid { get; set; }
}

public class CreateClientRequest
{
    public string Type { get; set; } = "Individual";
    public string Name { get; set; }
    public string IdNumber { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Country { get; set; } = "BY";
    public bool IsResident { get; set; } = true;
    public string KycRiskLevel { get; set; } = "Low";
    public string Notes { get; set; }
    /// <summary>Данные, полученные из МСИ с согласия клиента (требование 1).</summary>
    public bool UseMsi { get; set; }
}

public class UpdateKycRequest
{
    public string RiskLevel { get; set; }
    public string Notes { get; set; }
    public DateOnly? ReviewDate { get; set; }
}

public class AddRepresentativeRequest
{
    public string FullName { get; set; }
    public string IdNumber { get; set; }
    public string Authority { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
}
