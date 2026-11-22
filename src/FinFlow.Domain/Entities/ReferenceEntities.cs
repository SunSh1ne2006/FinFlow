using FinFlow.Domain.Common;

namespace FinFlow.Domain.Entities;

/// <summary>Справочник параметров с датой действия (ставка налога, ставка до востребования).</summary>
public class Parameter : BaseEntity
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal Value { get; set; }
    public string TextValue { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Запись журнала аудита. Только вставка, хранение не менее 5 лет.</summary>
public class AuditRecord : BaseEntity
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string UserLogin { get; set; }
    public string Action { get; set; }
    public string EntityName { get; set; }
    public string EntityId { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string IpAddress { get; set; }
}
