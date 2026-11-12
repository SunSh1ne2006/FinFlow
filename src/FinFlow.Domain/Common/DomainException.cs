namespace FinFlow.Domain.Common;

/// <summary>Нарушение бизнес-правила предметной области.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
