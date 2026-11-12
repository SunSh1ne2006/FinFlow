using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Administrator")]
public class UsersController : ControllerBase
{
    private readonly AdminService _admin;

    public UsersController(AdminService admin)
    {
        _admin = admin;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _admin.ListUsersAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken) =>
        Ok(await _admin.CreateUserAsync(request, cancellationToken));

    [HttpPost("{id:guid}/block")]
    public async Task<ActionResult<UserDto>> Block(Guid id, [FromQuery] bool blocked, CancellationToken cancellationToken) =>
        Ok(await _admin.SetBlockedAsync(id, blocked, cancellationToken));
}
