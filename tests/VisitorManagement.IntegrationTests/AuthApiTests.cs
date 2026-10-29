using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using VisitorManagement.Application.DTOs.Auth;
using VisitorManagement.IntegrationTests.Support;

namespace VisitorManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Valid_login_returns_a_token_and_the_role()
    {
        var response = await factory.NewClient().PostAsJsonAsync(
            "/api/auth/login", new { username = "admin", password = "Admin@12345" }, ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await response.ReadAsync<LoginResponse>();
        login.Token.Should().NotBeNullOrWhiteSpace();
        login.Role.Should().Be("Admin");
        login.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Wrong_password_returns_401_with_the_friendly_message_and_no_token()
    {
        var response = await factory.NewClient().PostAsJsonAsync(
            "/api/auth/login", new { username = "admin", password = "not-the-password" }, ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.ReadProblemAsync();
        problem.Code.Should().Be("invalid_credentials");
        problem.Detail.Should().Contain("Invalid username or password");
        problem.CorrelationId.Should().Be(response.CorrelationId());
    }

    [Fact]
    public async Task Unknown_user_gets_the_same_answer_as_a_wrong_password()
    {
        var wrongPassword = await factory.NewClient().PostAsJsonAsync(
            "/api/auth/login", new { username = "admin", password = "nope" }, ApiTestSupport.Json);
        var unknownUser = await factory.NewClient().PostAsJsonAsync(
            "/api/auth/login", new { username = "ghost", password = "nope" }, ApiTestSupport.Json);

        unknownUser.StatusCode.Should().Be(wrongPassword.StatusCode);
        (await unknownUser.ReadProblemAsync()).Detail.Should().Be((await wrongPassword.ReadProblemAsync()).Detail);
    }

    [Fact]
    public async Task Empty_login_body_returns_400_with_field_errors()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/login", new { }, ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Should().ContainKey("username");
    }

    [Fact]
    public async Task Protected_endpoint_without_a_token_returns_a_json_401_with_a_correlation_id()
    {
        var response = await factory.NewClient().GetAsync("/api/visitors");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.ReadProblemAsync();
        problem.Code.Should().Be("unauthorized");
        problem.CorrelationId.Should().Be(response.CorrelationId());
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var client = factory.NewClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.real-token");

        var response = await client.GetAsync("/api/visitors");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_endpoint_reports_the_signed_in_role()
    {
        var user = await factory.UserAsync();

        var response = await user.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("User");
    }
}