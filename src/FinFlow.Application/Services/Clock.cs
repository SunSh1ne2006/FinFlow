using FinFlow.Application.Abstractions;

namespace FinFlow.Application.Services;

/// <summary>Системные часы. Дата берётся по часовому поясу сервера (операционный день банка).</summary>
public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
