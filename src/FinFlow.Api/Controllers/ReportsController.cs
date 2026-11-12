using System.Text;
using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Head,Accountant,Treasurer,RiskManager,Administrator")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reports;
    private readonly TaxService _tax;

    public ReportsController(ReportService reports, TaxService tax)
    {
        _reports = reports;
        _tax = tax;
    }

    [HttpGet("portfolio")]
    public async Task<ActionResult<PortfolioReportDto>> Portfolio(CancellationToken cancellationToken) =>
        Ok(await _reports.PortfolioAsync(cancellationToken));

    /// <summary>Выгрузка портфеля в CSV (требование 18).</summary>
    [HttpGet("portfolio.csv")]
    public async Task<IActionResult> PortfolioCsv(CancellationToken cancellationToken)
    {
        var csv = await _reports.PortfolioCsvAsync(cancellationToken);
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv);
        var payload = preamble.Concat(body).ToArray();
        return File(payload, "text/csv; charset=utf-8", $"portfolio-{DateTime.Today:yyyyMMdd}.csv");
    }

    /// <summary>Реестр обязательств перед вкладчиками – физическими лицами для АГВ.</summary>
    [HttpGet("agv")]
    public async Task<ActionResult<AgvRegisterDto>> Agv(CancellationToken cancellationToken) =>
        Ok(await _reports.AgvAsync(cancellationToken));

    [HttpGet("holds")]
    public async Task<ActionResult<IReadOnlyList<HoldDto>>> Holds(CancellationToken cancellationToken) =>
        Ok(await _reports.ActiveHoldsAsync(cancellationToken));

    [HttpGet("tax")]
    public async Task<ActionResult<TaxReportDto>> Tax(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var actualTo = to ?? DateOnly.FromDateTime(DateTime.Today);
        var actualFrom = from ?? actualTo.AddMonths(-1);
        return Ok(await _tax.ReportAsync(actualFrom, actualTo, cancellationToken));
    }
}
