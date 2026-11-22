using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Вход сотрудника банка в систему.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await _auth.LoginAsync(request, cancellationToken));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken) =>
        Ok(await _auth.GetCurrentAsync(cancellationToken));

    [HttpPost("logout")]
    [Authorize]
    public ActionResult<MessageDto> Logout()
    {
        var header = Request.Headers["Authorization"].ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
        _auth.Logout(token);
        return Ok(MessageDto.Ok("Сеанс завершён."));
    }
}
