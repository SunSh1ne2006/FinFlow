using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/margin")]
[Authorize(Roles = "Head,Treasurer,RiskManager,Administrator")]
public class MarginController : ControllerBase
{
    private readonly MarginService _margin;

    public MarginController(MarginService margin)
    {
        _margin = margin;
    }

    /// <summary>Средневзвешенная стоимость ресурсов и маржа (формулы 1.14–1.16).</summary>
    [HttpGet]
    public async Task<ActionResult<MarginReportDto>> Get([FromQuery] string scenario = "model", CancellationToken cancellationToken = default) =>
        Ok(await _margin.ReportAsync(scenario, cancellationToken));
}
