using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisitorManagement.Api.Authorization;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Auth;

namespace VisitorManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);

        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authentication failed",
                detail: "Invalid username or password. Please verify your credentials and try again.");
        }

        return Ok(result);
    }

    /// <summary>Returns the identity found in the token. Useful to verify roles.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.CanViewVisitors)]
    public IActionResult Me() => Ok(new
    {
        UserId = User.FindFirst("sub")?.Value,
        Username = User.Identity?.Name,
        Roles = User.FindAll("role").Select(c => c.Value)
    });
}