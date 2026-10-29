using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using VisitorManagement.Domain.Entities;
using VisitorManagement.Infrastructure.Security;

namespace VisitorManagement.UnitTests.Infrastructure;

public class JwtTokenGeneratorTests
{
    private static JwtOptions Options() => new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = new string('k', 40),
        ExpiryMinutes = 30
    };

    [Fact]
    public void Generate_puts_the_user_id_role_issuer_and_expiry_in_the_token()
    {
        var user = new User { Id = 7, Username = "admin", Role = new Role { Name = "Admin" } };

        var (token, expiresAtUtc) = new JwtTokenGenerator(Options()).Generate(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == "7");
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Admin");
        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");
        expiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Generate_never_puts_the_password_hash_in_the_token()
    {
        var user = new User { Id = 7, Username = "admin", PasswordHash = "SECRET-HASH", Role = new Role { Name = "Admin" } };

        var (token, _) = new JwtTokenGenerator(Options()).Generate(user);

        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Should().NotContain(c => c.Value.Contains("SECRET-HASH"));
    }
}

public class JwtOptionsTests
{
    private static IConfiguration Config(string signingKey) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:SigningKey"] = signingKey,
            ["Jwt:ExpiryMinutes"] = "60"
        })
        .Build();

    [Fact]
    public void Load_fails_fast_when_the_signing_key_is_too_short() =>
        FluentActions.Invoking(() => JwtOptions.Load(Config("short")))
            .Should().Throw<InvalidOperationException>().WithMessage("*SigningKey*");

    [Fact]
    public void Load_fails_fast_when_the_signing_key_is_missing() =>
        FluentActions.Invoking(() => JwtOptions.Load(Config("")))
            .Should().Throw<InvalidOperationException>().WithMessage("*SigningKey*");

    [Fact]
    public void Load_succeeds_with_a_long_enough_key() =>
        JwtOptions.Load(Config(new string('k', 40))).ExpiryMinutes.Should().Be(60);
}

public class PasswordVerifierTests
{
    private readonly PasswordHasher<User> _hasher = new();

    [Fact]
    public void Verify_accepts_the_right_password_and_rejects_a_wrong_one()
    {
        var user = new User { Username = "admin" };
        user.PasswordHash = _hasher.HashPassword(user, "Admin@12345");
        var verifier = new PasswordVerifier(_hasher);

        verifier.Verify(user, "Admin@12345").Should().BeTrue();
        verifier.Verify(user, "Admin@12346").Should().BeFalse();
    }

    [Fact]
    public void Stored_hash_is_not_the_plain_password()
    {
        var user = new User { Username = "admin" };

        var hash = _hasher.HashPassword(user, "Admin@12345");

        hash.Should().NotContain("Admin@12345");
    }
}