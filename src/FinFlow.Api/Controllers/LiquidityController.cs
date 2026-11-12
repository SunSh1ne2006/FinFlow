using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/liquidity")]
[Authorize(Roles = "Treasurer,Head,RiskManager,Administrator")]
public class LiquidityController : ControllerBase
{
    private readonly LiquidityService _liquidity;

    public LiquidityController(LiquidityService liquidity)
    {
        _liquidity = liquidity;
    }

    /// <summary>Прогноз оттока, запаса и разрыва ликвидности по формулам (1.11)–(1.13).</summary>
    [HttpGet("forecast")]
    public async Task<ActionResult<LiquidityForecastDto>> Forecast(
        [FromQuery] int days = 90,
        [FromQuery] string scenario = "db",
        CancellationToken cancellationToken = default) =>
        Ok(await _liquidity.ForecastAsync(days, scenario, cancellationToken));

    [HttpGet("settings")]
    public async Task<ActionResult<LiquiditySettingsDto>> Settings(CancellationToken cancellationToken) =>
        Ok(await _liquidity.GetSettingsAsync(cancellationToken));

    [HttpPut("settings")]
    [Authorize(Roles = "Treasurer,Head,Administrator")]
    public async Task<ActionResult<LiquiditySettingsDto>> UpdateSettings([FromBody] LiquiditySettingsDto request, CancellationToken cancellationToken) =>
        Ok(await _liquidity.UpdateSettingsAsync(request, cancellationToken));
}
