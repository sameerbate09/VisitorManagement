using VisitorManagement.Application.DTOs.Auth;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetActiveByUsernameAsync(string username, CancellationToken ct);
}

public interface IPasswordVerifier
{
    bool Verify(User user, string password);
}

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) Generate(User user);
}

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
}