using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Стоимость ресурсов и процентная маржа (требование 15, п. 1.3.5):
/// r̄ (1.14), M (1.15), П = M − Rрез (1.16).
/// </summary>
public class MarginService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly ParameterService _parameters;
    private readonly RiskService _risks;

    public MarginService(IBankDbContext db, IClock clock, ParameterService parameters, RiskService risks)
    {
        _db = db;
        _clock = clock;
        _parameters = parameters;
        _risks = risks;
    }

    public async Task<MarginReportDto> ReportAsync(string scenario, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var transferRate = await _parameters.GetValueAsync(ParameterService.TransferRate, today, ModelBankData.TransferRatePercent, cancellationToken);
        var costPercent = await _parameters.GetValueAsync(ParameterService.OperatingCost, today, ModelBankData.OperatingCostPercent, cancellationToken);
        var reserve = await _risks.GetReserveAsync(cancellationToken);

        var isModel = string.Equals(scenario, "model", StringComparison.OrdinalIgnoreCase);
        var segments = isModel
            ? ModelBankData.PortfolioSegments
            : await BuildPortfolioSegmentsAsync(cancellationToken);

        var report = MarginCalculator.Report(segments, transferRate, costPercent, reserve);

        return new MarginReportDto
        {
            Scenario = isModel ? "model" : "db",
            Unit = isModel ? "тыс. р." : "р.",
            TotalVolume = report.TotalVolume,
            WeightedRate = report.WeightedRate,
            AnnualInterestExpense = report.AnnualInterestExpense,
            TransferRate = report.TransferRate,
            CostPercent = report.CostPercent,
            Margin = report.Margin,
            RiskReserve = report.RiskReserve,
            NetMargin = report.NetMargin,
            Segments = report.Segments.Select(s => new MarginSegmentDto
            {
                Name = s.Name,
                Volume = s.Volume,
                RatePercent = s.RatePercent,
                InterestExpense = s.InterestExpense
            }).ToList(),
            CalculatedAt = _clock.UtcNow
        };
    }

    private async Task<IReadOnlyList<PortfolioSegment>> BuildPortfolioSegmentsAsync(CancellationToken cancellationToken)
    {
        var contracts = await _db.Contracts
            .Include(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .Include(c => c.Account)
            .Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Prolonged)
            .ToListAsync(cancellationToken);

        var groups = contracts
            .Where(c => c.ProductVersion != null)
            .GroupBy(c => new
            {
                Product = c.ProductVersion.Product?.Name ?? "Прочее",
                Currency = c.Account?.Currency ?? "BYN"
            })
            .Select(g =>
            {
                var volume = g.Sum(c => c.Principal);
                var weighted = volume <= 0m
                    ? g.Average(c => c.ProductVersion.RatePercent)
                    : g.Sum(c => c.Principal * c.ProductVersion.RatePercent) / volume;

                return new PortfolioSegment
                {
                    Name = $"{g.Key.Product} ({g.Key.Currency})",
                    Volume = Money.Round(volume),
                    RatePercent = Money.Percent(weighted)
                };
            })
            .OrderByDescending(s => s.Volume)
            .ToList();

        if (groups.Count == 0)
        {
            groups.Add(new PortfolioSegment { Name = "Портфель не сформирован", Volume = 0m, RatePercent = 0m });
        }

        return groups;
    }
}
