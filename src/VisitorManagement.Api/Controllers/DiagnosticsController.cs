using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisitorManagement.Api.Authorization;
using VisitorManagement.Application.Exceptions;

namespace VisitorManagement.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public DiagnosticsController(IWebHostEnvironment environment) => _environment = environment;

    /// <summary>Development only. Simulates an unexpected server failure.</summary>
    [HttpGet("error")]
    [Authorize(Policy = Policies.CanManageVisitors)]
    [ApiExplorerSettings(IgnoreApi = false)]
    public IActionResult SimulateError()
    {
        if (!_environment.IsDevelopment())
            throw new NotFoundException("Endpoint", "diagnostics/error");

        throw new InvalidOperationException("Simulated failure for diagnostics.");
    }
}