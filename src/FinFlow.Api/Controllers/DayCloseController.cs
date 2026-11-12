using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/dayclose")]
[Authorize(Roles = "Accountant,Administrator,Head")]
public class DayCloseController : ControllerBase
{
    private readonly DayCloseService _dayClose;

    public DayCloseController(DayCloseService dayClose)
    {
        _dayClose = dayClose;
    }

    [HttpGet("current")]
    public async Task<ActionResult<OperationalDayDto>> Current(CancellationToken cancellationToken) =>
        Ok(await _dayClose.GetCurrentAsync(cancellationToken));

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<OperationalDayDto>>> History([FromQuery] int take = 30, CancellationToken cancellationToken = default) =>
        Ok(await _dayClose.HistoryAsync(take, cancellationToken));

    [HttpPost("start")]
    public async Task<ActionResult<OperationalDayDto>> Start(CancellationToken cancellationToken) =>
        Ok(await _dayClose.StartAsync(cancellationToken));

    [HttpPost("continue")]
    public async Task<ActionResult<OperationalDayDto>> Continue(CancellationToken cancellationToken) =>
        Ok(await _dayClose.ContinueAsync(cancellationToken));

    [HttpGet("reconciliation")]
    public async Task<ActionResult<ReconciliationDto>> Reconciliation([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var actual = date ?? DateOnly.FromDateTime(DateTime.Today);
        return Ok(await _dayClose.ReconcileAsync(actual, cancellationToken));
    }
}
