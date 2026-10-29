using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Auth;
using VisitorManagement.Application.Services;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.UnitTests.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordVerifier _verifier = Substitute.For<IPasswordVerifier>();
    private readonly IJwtTokenGenerator _generator = Substitute.For<IJwtTokenGenerator>();
    private readonly AuthService _sut;

    private static readonly User Admin = new()
    {
        Id = 1,
        Username = "admin",
        DisplayName = "Demo Admin",
        PasswordHash = "hash",
        Role = new Role { Id = 1, Name = "Admin" }
    };

    public AuthServiceTests() =>
        _sut = new AuthService(_users, _verifier, _generator, NullLogger<AuthService>.Instance);

    [Fact]
    public async Task LoginAsync_with_valid_credentials_returns_a_token_and_the_role()
    {
        var expires = DateTime.UtcNow.AddHours(1);
        _users.GetActiveByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(Admin);
        _verifier.Verify(Admin, "Admin@12345").Returns(true);
        _generator.Generate(Admin).Returns(("jwt-token", expires));

        var result = await _sut.LoginAsync(
            new LoginRequest { Username = "admin", Password = "Admin@12345" }, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Token.Should().Be("jwt-token");
        result.ExpiresAtUtc.Should().Be(expires);
        result.Username.Should().Be("admin");
        result.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task LoginAsync_with_a_wrong_password_returns_null_and_issues_no_token()
    {
        _users.GetActiveByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(Admin);
        _verifier.Verify(Admin, Arg.Any<string>()).Returns(false);

        var result = await _sut.LoginAsync(
            new LoginRequest { Username = "admin", Password = "wrong" }, CancellationToken.None);

        result.Should().BeNull();
        _generator.DidNotReceiveWithAnyArgs().Generate(default!);
    }

    [Fact]
    public async Task LoginAsync_with_an_unknown_user_returns_null_without_checking_a_password()
    {
        _users.GetActiveByUsernameAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _sut.LoginAsync(
            new LoginRequest { Username = "ghost", Password = "whatever" }, CancellationToken.None);

        result.Should().BeNull();
        _verifier.DidNotReceiveWithAnyArgs().Verify(default!, default!);
    }

    [Fact]
    public async Task LoginAsync_trims_the_username_before_lookup()
    {
        _users.GetActiveByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(Admin);
        _verifier.Verify(Admin, Arg.Any<string>()).Returns(true);
        _generator.Generate(Admin).Returns(("jwt-token", DateTime.UtcNow.AddHours(1)));

        var result = await _sut.LoginAsync(
            new LoginRequest { Username = "  admin  ", Password = "Admin@12345" }, CancellationToken.None);

        result.Should().NotBeNull();
    }
}