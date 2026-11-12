namespace FinFlow.Domain.Common;

/// <summary>
/// Базовые служебные поля всех сущностей (п. 2.2.1 записки):
/// Id (последовательный GUID), RowVersion для оптимистической блокировки, CreatedAt, CreatedBy.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
}
