using System.Security.Claims;
using FinFlow.Api.Common;
using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

/// <summary>
/// Личный кабинет клиента (интернет-банк): вход по паспортным данным, свои вклады
/// и опубликованные условия вкладов банка. Доступ проверяется отдельной схемой
/// аутентификации клиентов.
/// </summary>
[ApiController]
[Route("api/portal")]
public class ClientPortalController : ControllerBase
{
    private const string ClientScheme = ClientTokenAuthenticationHandler.SchemeName;

    private readonly ClientPortalService _portal;

    public ClientPortalController(ClientPortalService portal)
    {
        _portal = portal;
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<ClientLoginResponse>> Login([FromBody] ClientLoginRequest request, CancellationToken cancellationToken) =>
        Ok(await _portal.LoginAsync(request, cancellationToken));

    [HttpPost("auth/logout")]
    [Authorize(AuthenticationSchemes = ClientScheme)]
    public IActionResult Logout()
    {
        var header = Request.Headers["Authorization"].ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header.Substring("Bearer ".Length).Trim()
            : null;

        _portal.Logout(token);
        return Ok(MessageDto.Ok("Сеанс личного кабинета завершён."));
    }

    [HttpGet("profile")]
    [Authorize(AuthenticationSchemes = ClientScheme)]
    public async Task<ActionResult<ClientPortalProfileDto>> Profile(CancellationToken cancellationToken)
    {
        var clientId = CurrentClientId();
        return clientId.HasValue
            ? Ok(await _portal.GetProfileAsync(clientId.Value, cancellationToken))
            : Unauthorized();
    }

    [HttpGet("deposits")]
    [Authorize(AuthenticationSchemes = ClientScheme)]
    public async Task<ActionResult<ClientDepositsDto>> Deposits(CancellationToken cancellationToken)
    {
        var clientId = CurrentClientId();
        return clientId.HasValue
            ? Ok(await _portal.GetDepositsAsync(clientId.Value, cancellationToken))
            : Unauthorized();
    }

    [HttpGet("deposits/{contractId:guid}")]
    [Authorize(AuthenticationSchemes = ClientScheme)]
    public async Task<ActionResult<ClientDepositDetailsDto>> Deposit(Guid contractId, CancellationToken cancellationToken)
    {
        var clientId = CurrentClientId();
        return clientId.HasValue
            ? Ok(await _portal.GetDepositAsync(clientId.Value, contractId, cancellationToken))
            : Unauthorized();
    }

    [HttpGet("offers")]
    [Authorize(AuthenticationSchemes = ClientScheme)]
    public async Task<ActionResult<ClientOffersDto>> Offers(CancellationToken cancellationToken)
    {
        var clientId = CurrentClientId();
        return clientId.HasValue
            ? Ok(await _portal.GetOffersAsync(clientId.Value, cancellationToken))
            : Unauthorized();
    }

    private Guid? CurrentClientId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
