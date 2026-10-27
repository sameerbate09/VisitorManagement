using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisitorManagement.Api.Authorization;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Visitors;

namespace VisitorManagement.Api.Controllers;

[ApiController]
[Route("api/visitors")]
public class VisitorsController : ControllerBase
{
    private readonly IVisitorService _service;

    public VisitorsController(IVisitorService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = Policies.CanViewVisitors)]
    [ProducesResponseType(typeof(PagedResult<VisitorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VisitorDto>>> List(
        [FromQuery] VisitorListQuery query, CancellationToken ct) =>
        Ok(await _service.ListAsync(query, ct));

    [HttpGet("{id:int}", Name = "GetVisitor")]
    [Authorize(Policy = Policies.CanViewVisitors)]
    [ProducesResponseType(typeof(VisitorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VisitorDto>> Get(int id, CancellationToken ct) =>
        Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.CanManageVisitors)]
    [ProducesResponseType(typeof(VisitorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VisitorDto>> Create(CreateVisitorRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtRoute("GetVisitor", new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.CanManageVisitors)]
    [ProducesResponseType(typeof(VisitorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VisitorDto>> Update(int id, UpdateVisitorRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, request, ct));
}