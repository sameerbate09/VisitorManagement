using Microsoft.AspNetCore.Identity;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.Infrastructure.Security;

public class PasswordVerifier : IPasswordVerifier
{
    private readonly IPasswordHasher<User> _hasher;

    public PasswordVerifier(IPasswordHasher<User> hasher) => _hasher = hasher;

    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password)
            != PasswordVerificationResult.Failed;
}