using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Справочник параметров с датой действия: к расчёту применяется значение,
/// действующее на соответствующую дату (ставка налога, ставка до востребования, ставка рефинансирования).
/// </summary>
public class ParameterService
{
    public const string TaxRate = "TaxRate";
    public const string DemandRate = "DemandRate";
    public const string RefinancingRate = "RefinancingRate";

    private readonly IBankDbContext _db;

    public ParameterService(IBankDbContext db)
    {
        _db = db;
    }

    /// <summary>Значение параметра на дату. Исключение, если параметр не настроен.</summary>
    public async Task<decimal> GetValueAsync(string code, DateOnly date, CancellationToken cancellationToken = default)
    {
        var value = await FindAsync(code, date, cancellationToken);
        if (!value.HasValue)
        {
            throw new AppException($"Параметр «{code}» не настроен на дату {date:dd.MM.yyyy}.", 500);
        }

        return value.Value;
    }

    public async Task<decimal> GetValueAsync(string code, DateOnly date, decimal fallback, CancellationToken cancellationToken = default)
    {
        var value = await FindAsync(code, date, cancellationToken);
        return value ?? fallback;
    }

    private async Task<decimal?> FindAsync(string code, DateOnly date, CancellationToken cancellationToken)
    {
        var parameter = await _db.Parameters
            .Where(p => p.Code == code && p.IsActive && p.ValidFrom <= date)
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        return parameter?.Value;
    }
}
