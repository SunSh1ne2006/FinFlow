using FinFlow.Application.Services;
using FinFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Administrator,Head")]
public class AuditController : ControllerBase
{
    private readonly AuditService _audit;

    public AuditController(AuditService audit)
    {
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditRecordDto>>> Query(
        [FromQuery] string entity,
        [FromQuery] string action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _audit.QueryAsync(entity, action, page, pageSize, cancellationToken));
}
