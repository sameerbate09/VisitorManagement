using System.Net;
using System.Text.Json;
using FluentAssertions;
using VisitorManagement.IntegrationTests.Support;

namespace VisitorManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PipelineApiTests(ApiFactory factory)
{
    [Fact]
    public async Task An_unexpected_failure_returns_a_safe_500_with_a_reference_and_no_internals()
    {
        var admin = await factory.AdminAsync();

        var response = await admin.GetAsync("/api/diagnostics/error");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ApiProblem>(body, ApiTestSupport.Json)!;

        problem.Code.Should().Be("unexpected_error");
        problem.CorrelationId.Should().Be(response.CorrelationId());
        problem.Detail.Should().Contain($"Reference: {response.CorrelationId()}");

        // Nothing technical leaks to the caller.
        body.Should().NotContain("Simulated failure")
            .And.NotContain("InvalidOperationException")
            .And.NotContain("StackTrace");
    }

    [Fact]
    public async Task A_response_always_carries_a_correlation_id_even_for_unknown_routes()
    {
        var admin = await factory.AdminAsync();

        var response = await admin.GetAsync("/api/does-not-exist");

        response.Headers.Contains(ApiTestSupport.CorrelationHeader).Should().BeTrue();
    }

    [Fact]
    public async Task An_unsafe_incoming_correlation_id_is_replaced_by_a_generated_one()
    {
        var client = factory.NewClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(ApiTestSupport.CorrelationHeader, "bad id!!");

        var response = await client.GetAsync("/api/visitors");

        response.CorrelationId().Should().NotBe("bad id!!").And.MatchRegex("^[a-f0-9]{32}$");
    }

    [Fact]
    public async Task Cors_allows_the_angular_origin_and_exposes_the_correlation_header()
    {
        var response = await Preflight("http://localhost:4200");

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origin).Should().BeTrue();
        origin!.Single().Should().Be("http://localhost:4200");
    }

    [Fact]
    public async Task Cors_does_not_allow_other_origins()
    {
        var response = await Preflight("https://evil.example");

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private Task<HttpResponseMessage> Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/visitors");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-correlation-id");
        return factory.NewClient().SendAsync(request);
    }
}