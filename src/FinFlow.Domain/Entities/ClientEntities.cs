using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Entities;

/// <summary>Клиент банка: физическое лицо, юридическое лицо или ИП.</summary>
public class Client : BaseEntity
{
    public ClientType Type { get; set; } = ClientType.Individual;
    public string Name { get; set; }
    public string IdNumber { get; set; }
    /// <summary>Дата рождения из паспорта: контрольный реквизит входа в личный кабинет.</summary>
    public DateOnly? BirthDate { get; set; }
    public string Country { get; set; } = "BY";
    public bool IsResident { get; set; } = true;
    public string Phone { get; set; }
    public string Email { get; set; }
    public IdentificationStatus IdentificationStatus { get; set; } = IdentificationStatus.NotIdentified;
    public DateTime? IdentifiedAt { get; set; }
    public string Notes { get; set; }

    public List<Account> Accounts { get; set; } = new();

    /// <summary>Операции невозможны без идентифицированного клиента.</summary>
    public bool IsIdentified => IdentificationStatus == IdentificationStatus.Identified;
}
