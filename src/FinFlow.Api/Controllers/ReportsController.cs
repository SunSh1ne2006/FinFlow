using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Head")]
public class ReportsController : ControllerBase
{
    private readonly StaffReportService _reports;

    public ReportsController(StaffReportService reports)
    {
        _reports = reports;
    }

    /// <summary>Отчёт о работе сотрудников за период: клиенты, счета, вклады, операции, закрытия дня.</summary>
    [HttpGet("staff")]
    public async Task<ActionResult<StaffReportDto>> Staff(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken) =>
        Ok(await _reports.BuildAsync(from, to, cancellationToken));
}
