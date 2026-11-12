using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/meta")]
[Authorize]
public class MetaController : ControllerBase
{
    private readonly MetaService _meta;
    private readonly AdminService _admin;

    public MetaController(MetaService meta, AdminService admin)
    {
        _meta = meta;
        _admin = admin;
    }

    [HttpGet]
    public async Task<ActionResult<MetaDto>> Get(CancellationToken cancellationToken) => Ok(await _meta.GetAsync(cancellationToken));

    [HttpGet("roles")]
    public ActionResult<IReadOnlyList<RoleInfoDto>> Roles() => Ok(_admin.Roles());
}
