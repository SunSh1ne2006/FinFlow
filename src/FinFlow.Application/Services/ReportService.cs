using System.Globalization;
using System.Text;
using FinFlow.Application.Abstractions;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Common;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Отчёты (требование 18, сокращённый объём MVP): портфель, реестр обязательств для АГВ,
/// действующие ограничения, выгрузка в CSV.
/// </summary>
public class ReportService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;

    public ReportService(IBankDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PortfolioReportDto> PortfolioAsync(CancellationToken cancellationToken = default)
    {
        var contracts = await _db.Contracts
            .Include(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .Include(c => c.Account)
            .Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Prolonged)
            .ToListAsync(cancellationToken);

        var segments = contracts
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

                return new PortfolioSegmentReportDto
                {
                    Name = g.Key.Product,
                    Currency = g.Key.Currency,
                    Volume = Money.Round(volume),
                    RatePercent = Money.Percent(weighted),
                    Contracts = g.Count()
                };
            })
            .OrderByDescending(s => s.Volume)
            .ToList();

        return new PortfolioReportDto
        {
            AsOf = _clock.Today,
            TotalContracts = contracts.Count,
            TotalPrincipal = Money.Round(contracts.Sum(c => c.Principal)),
            TotalAccruedInterest = Money.Round(contracts.Sum(c => c.AccruedInterest)),
            Segments = segments
        };
    }

    /// <summary>Реестр обязательств перед вкладчиками – физическими лицами для АГВ (требование 18).</summary>
    public async Task<AgvRegisterDto> AgvAsync(CancellationToken cancellationToken = default)
    {
        var rows = await (
            from contract in _db.Contracts
            join client in _db.Clients on contract.ClientId equals client.Id
            join account in _db.Accounts on contract.AccountId equals account.Id
            where client.Type == ClientType.Individual
               && (contract.Status == ContractStatus.Active || contract.Status == ContractStatus.Prolonged)
               && contract.Principal > 0
            orderby client.Name
            select new AgvRegisterRowDto
            {
                ClientName = client.Name,
                IdNumber = client.IdNumber,
                ContractNumber = contract.Number,
                Currency = account.Currency,
                Amount = contract.Principal,
                AccruedInterest = contract.AccruedInterest,
                Total = contract.Principal + contract.AccruedInterest
            }).ToListAsync(cancellationToken);

        return new AgvRegisterDto
        {
            AsOf = _clock.Today,
            ClientsCount = rows.Select(r => r.IdNumber).Distinct().Count(),
            TotalAmount = Money.Round(rows.Where(r => r.Currency == "BYN").Sum(r => r.Total)),
            Rows = rows
        };
    }

    public async Task<IReadOnlyList<HoldDto>> ActiveHoldsAsync(CancellationToken cancellationToken = default)
    {
        var holds = await _db.Holds
            .Where(h => h.Status == HoldStatus.Active)
            .OrderByDescending(h => h.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return holds.Select(h => h.ToDto()).ToList();
    }

    public async Task<string> PortfolioCsvAsync(CancellationToken cancellationToken = default)
    {
        var portfolio = await PortfolioAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Наименование;Валюта;Объём;Ставка, %;Договоров");
        foreach (var segment in portfolio.Segments)
        {
            builder.AppendLine(string.Join(';',
                segment.Name,
                segment.Currency,
                segment.Volume.ToString("0.00", CultureInfo.InvariantCulture),
                segment.RatePercent.ToString("0.0000", CultureInfo.InvariantCulture),
                segment.Contracts.ToString(CultureInfo.InvariantCulture)));
        }

        builder.AppendLine();
        builder.AppendLine($"Итого;;{portfolio.TotalPrincipal:0.00};;{portfolio.TotalContracts}");
        return builder.ToString();
    }
}
