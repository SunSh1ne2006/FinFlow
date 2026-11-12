using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Application.Strategies;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Реестр рисков и количественная оценка (требование 14, п. 1.9):
/// R = pб × Q (1.17), EMV (1.18), σ (1.19), резерв P80 (1.20), матрица 5×5,
/// диаграмма «торнадо» по данным анализа чувствительности (таблица 1.18).
/// </summary>
public class RiskService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _current;
    private readonly IReserveStrategy _reserveStrategy;

    public RiskService(IBankDbContext db, IClock clock, IAuditWriter audit, ICurrentUser current, IReserveStrategy reserveStrategy)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _current = current;
        _reserveStrategy = reserveStrategy;
    }

    public async Task<IReadOnlyList<RiskDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var risks = await _db.Risks
            .Include(r => r.Assessments)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

        return risks.Select(r => r.ToDto(_clock.Today)).ToList();
    }

    public async Task<RiskDto> AssessAsync(Guid riskId, AssessRiskRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new AppException("Не переданы параметры оценки риска.");
        }

        if (request.Probability is < 0m or > 1m)
        {
            throw new AppException("Вероятность риска должна находиться в диапазоне от 0 до 1.");
        }

        if (request.MitigatedProbability.HasValue && request.MitigatedProbability.Value is < 0m or > 1m)
        {
            throw new AppException("Вероятность после мероприятий должна находиться в диапазоне от 0 до 1.");
        }

        if (request.MoneyImpact < 0m)
        {
            throw new AppException("Денежная оценка последствий не может быть отрицательной.");
        }

        var risk = await _db.Risks
            .Include(r => r.Assessments)
            .FirstOrDefaultAsync(r => r.Id == riskId, cancellationToken);

        if (risk == null)
        {
            throw new NotFoundException("Риск не найден.");
        }

        var assessment = new RiskAssessment
        {
            RiskId = risk.Id,
            Date = _clock.Today,
            Probability = request.Probability,
            MoneyImpact = request.MoneyImpact,
            MitigatedProbability = request.MitigatedProbability,
            Notes = request.Notes,
            CreatedBy = _current.UserId
        };

        _db.RiskAssessments.Add(assessment);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync("RiskAssessed", nameof(Risk), risk.Id.ToString(), null,
            new { request.Probability, request.MoneyImpact, request.MitigatedProbability }, cancellationToken);

        return risk.ToDto(_clock.Today);
    }

    public async Task<RiskSummaryDto> SummaryAsync(CancellationToken cancellationToken = default)
    {
        var risks = await ListAsync(cancellationToken);
        var inputs = risks.Select(r => new RiskInput
        {
            Code = r.Code,
            Probability = r.Probability,
            MoneyImpact = r.MoneyImpact,
            MitigatedProbability = r.MitigatedProbability
        }).ToList();

        var summary = RiskCalculator.Summarize(inputs);
        var matrix = BuildMatrix(summary);

        return new RiskSummaryDto
        {
            ExpectedMonetaryValue = summary.ExpectedMonetaryValue,
            StandardDeviation = summary.StandardDeviation,
            Reserve = _reserveStrategy.Reserve(summary.ExpectedMonetaryValue, summary.StandardDeviation),
            Z = summary.Z,
            ResidualEmv = summary.ResidualEmv,
            MitigationEffect = summary.MitigationEffect,
            Risks = risks,
            Matrix = matrix,
            Sensitivity = ModelBankData.Sensitivity.Select(s => new SensitivityDto
            {
                Factor = s.Factor,
                BaseValue = s.BaseValue,
                Change = s.Change,
                Impact = s.Impact,
                Indicator = s.Indicator,
                ImpactValue = s.ImpactValue
            }).ToList(),
            CalculatedAt = _clock.UtcNow
        };
    }

    /// <summary>Резерв P80 для формулы (1.16) — используется при расчёте маржи.</summary>
    public async Task<decimal> GetReserveAsync(CancellationToken cancellationToken = default)
    {
        var summary = await SummaryAsync(cancellationToken);
        return summary.Reserve;
    }

    private static IReadOnlyList<RiskMatrixCellDto> BuildMatrix(RiskSummary summary)
    {
        var cells = new List<RiskMatrixCellDto>();

        for (var probability = 5; probability >= 1; probability--)
        {
            for (var impact = 1; impact <= 5; impact++)
            {
                var score = RiskCalculator.Score(probability, impact);
                var zone = RiskCalculator.Zone(score);
                var codes = summary.Risks
                    .Where(r => r.ProbabilityScore == probability && r.ImpactScore == impact)
                    .Select(r => r.Code)
                    .ToList();

                cells.Add(new RiskMatrixCellDto
                {
                    ProbabilityScore = probability,
                    ImpactScore = impact,
                    Score = score,
                    Zone = zone.ToString(),
                    ZoneText = zone.DisplayName(),
                    RiskCodes = codes
                });
            }
        }

        return cells;
    }
}
