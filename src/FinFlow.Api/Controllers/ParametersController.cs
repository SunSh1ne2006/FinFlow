using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/parameters")]
[Authorize(Roles = "Administrator,Head,Treasurer,Accountant")]
public class ParametersController : ControllerBase
{
    private readonly ParameterService _parameters;

    public ParametersController(ParameterService parameters)
    {
        _parameters = parameters;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParameterDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _parameters.ListAsync(cancellationToken));

    /// <summary>Новое значение параметра с датой действия (бизнес-правило 14).</summary>
    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ParameterDto>> Set([FromBody] SetParameterRequest request, CancellationToken cancellationToken) =>
        Ok(await _parameters.SetAsync(request, cancellationToken));
}
