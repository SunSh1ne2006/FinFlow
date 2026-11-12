using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/risks")]
[Authorize(Roles = "RiskManager,Head,Treasurer,Administrator")]
public class RisksController : ControllerBase
{
    private readonly RiskService _risks;

    public RisksController(RiskService risks)
    {
        _risks = risks;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RiskDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _risks.ListAsync(cancellationToken));

    /// <summary>Реестр, EMV, σ, резерв P80, матрица рисков и анализ чувствительности.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<RiskSummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await _risks.SummaryAsync(cancellationToken));

    [HttpPost("{id:guid}/assessments")]
    [Authorize(Roles = "RiskManager,Head,Administrator")]
    public async Task<ActionResult<RiskDto>> Assess(Guid id, [FromBody] AssessRiskRequest request, CancellationToken cancellationToken) =>
        Ok(await _risks.AssessAsync(id, request, cancellationToken));
}
