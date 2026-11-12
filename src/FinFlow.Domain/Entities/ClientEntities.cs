using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>Клиент банка: физическое лицо, юридическое лицо или ИП (требование 1).</summary>
public class Client : BaseEntity
{
    public ClientType Type { get; set; } = ClientType.Individual;
    public string Name { get; set; }
    public string IdNumber { get; set; }
    public string Country { get; set; } = "BY";
    public bool IsResident { get; set; } = true;
    public string Phone { get; set; }
    public string Email { get; set; }
    public IdentificationStatus IdentificationStatus { get; set; } = IdentificationStatus.NotIdentified;
    public DateTime? IdentifiedAt { get; set; }
    public string Notes { get; set; }

    public KycProfile KycProfile { get; set; }
    public List<Representative> Representatives { get; set; } = new();
    public List<Account> Accounts { get; set; } = new();

    /// <summary>Операции невозможны без идентифицированного клиента (бизнес-правило 4).</summary>
    public bool IsIdentified => IdentificationStatus == Enums.IdentificationStatus.Identified;
}

/// <summary>Анкета ПОД/ФТ (Закон № 165-З).</summary>
public class KycProfile : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; }
    public KycRiskLevel RiskLevel { get; set; } = KycRiskLevel.Low;
    public DateOnly ReviewDate { get; set; }
    public string Source { get; set; } = "ЦБУ";
    public string Notes { get; set; }
    public bool IsActual { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Анкета считается актуальной 12 месяцев.</summary>
    public bool IsOverdue(DateOnly today) => ReviewDate.AddYears(1) < today;
}

/// <summary>Представитель клиента и доверенность.</summary>
public class Representative : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; }
    public string FullName { get; set; }
    public string IdNumber { get; set; }
    public string Authority { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }

    public bool IsValidOn(DateOnly date) => ValidFrom <= date && ValidTo >= date;
}
