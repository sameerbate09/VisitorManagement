using Microsoft.Extensions.Logging;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Auth;

namespace VisitorManagement.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordVerifier _passwordVerifier;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IPasswordVerifier passwordVerifier,
        IJwtTokenGenerator tokenGenerator,
        ILogger<AuthService> logger)
    {
        _users = users;
        _passwordVerifier = passwordVerifier;
        _tokenGenerator = tokenGenerator;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var user = await _users.GetActiveByUsernameAsync(username, ct);

        // Same outcome for "unknown user" and "wrong password": no account enumeration.
        if (user is null || !_passwordVerifier.Verify(user, request.Password))
        {
            _logger.LogWarning(
                "Operation={Operation} failed: invalid credentials for username {Username}",
                "Login", username);
            return null;
        }

        var (token, expiresAtUtc) = _tokenGenerator.Generate(user);

        _logger.LogInformation(
            "Operation={Operation} succeeded for user {UserId} with role {Role}",
            "Login", user.Id, user.Role.Name);

        return new LoginResponse(token, expiresAtUtc, user.Username, user.DisplayName, user.Role.Name);
    }
}