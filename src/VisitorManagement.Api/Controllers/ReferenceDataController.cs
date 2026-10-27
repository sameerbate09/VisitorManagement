using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisitorManagement.Api.Authorization;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Visitors;

namespace VisitorManagement.Api.Controllers;

[ApiController]
[Authorize(Policy = Policies.CanViewVisitors)]
public class ReferenceDataController : ControllerBase
{
    private readonly IReferenceDataService _service;

    public ReferenceDataController(IReferenceDataService service) => _service = service;

    /// <summary>Type-ahead host search. Example: GET /api/hosts?search=an&amp;limit=10</summary>
    [HttpGet("api/hosts")]
    [ProducesResponseType(typeof(IReadOnlyList<HostDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HostDto>>> SearchHosts(
        [FromQuery] string? search, [FromQuery] int? limit, CancellationToken ct) =>
        Ok(await _service.SearchHostsAsync(search, limit, ct));

    [HttpGet("api/purposes")]
    [ProducesResponseType(typeof(IReadOnlyList<PurposeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PurposeDto>>> GetPurposes(CancellationToken ct) =>
        Ok(await _service.GetPurposesAsync(ct));
}