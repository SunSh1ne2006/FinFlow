using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Справочник параметров с датой действия (требование 16, бизнес-правило 14):
/// к операции применяется значение, действующее на её дату.
/// </summary>
public class ParameterService
{
    public const string TaxRate = "TaxRate";
    public const string DemandRate = "DemandRate";
    public const string RefinancingRate = "RefinancingRate";
    public const string RvsrByn1To3 = "RvsrByn1To3";
    public const string RvsrBynOver3 = "RvsrBynOver3";
    public const string OperatorLimit = "OperatorLimit";
    public const string ControllerLimit = "ControllerLimit";
    public const string AmlThreshold = "AmlThreshold";
    public const string ProlongationShare = "ProlongationShare";
    public const string EarlyWithdrawalShare = "EarlyWithdrawalShare";
    public const string OpeningCash = "OpeningCash";
    public const string MinimumCash = "MinimumCash";
    public const string ExpectedDailyInflow = "ExpectedDailyInflow";
    public const string TransferRate = "TransferRate";
    public const string OperatingCost = "OperatingCost";

    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public ParameterService(IBankDbContext db, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ParameterDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _db.Parameters
            .OrderBy(p => p.Code)
            .ThenByDescending(p => p.ValidFrom)
            .ToListAsync(cancellationToken);

        return items.Select(p => p.ToDto()).ToList();
    }

    /// <summary>Значение параметра на дату. Исключение, если параметр не настроен.</summary>
    public async Task<decimal> GetValueAsync(string code, DateOnly date, CancellationToken cancellationToken = default)
    {
        var parameter = await _db.Parameters
            .Where(p => p.Code == code && p.IsActive && p.ValidFrom <= date)
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (parameter == null)
        {
            throw new AppException($"Параметр «{code}» не настроен на дату {date:dd.MM.yyyy}.", 500);
        }

        return parameter.Value;
    }

    public async Task<decimal> GetValueAsync(string code, DateOnly date, decimal fallback, CancellationToken cancellationToken = default)
    {
        var parameter = await _db.Parameters
            .Where(p => p.Code == code && p.IsActive && p.ValidFrom <= date)
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        return parameter?.Value ?? fallback;
    }

    public async Task<ParameterDto> SetAsync(SetParameterRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code))
        {
            throw new AppException("Укажите код параметра.");
        }

        var validFrom = request.ValidFrom ?? _clock.Today;
        var entity = new Parameter
        {
            Code = request.Code.Trim(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? request.Code : request.Name.Trim(),
            Unit = request.Unit,
            Value = request.Value,
            TextValue = request.TextValue,
            ValidFrom = validFrom,
            IsActive = true
        };

        _db.Parameters.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ParameterSet", nameof(Parameter), entity.Id.ToString(), null,
            new { entity.Code, entity.Value, entity.ValidFrom }, cancellationToken);

        return entity.ToDto();
    }
}
