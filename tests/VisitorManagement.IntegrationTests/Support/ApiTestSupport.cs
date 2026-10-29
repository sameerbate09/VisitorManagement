using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using VisitorManagement.Application.DTOs.Auth;
using VisitorManagement.Application.DTOs.Visitors;

namespace VisitorManagement.IntegrationTests.Support;

/// <summary>The API's error contract: the same shape for every failure.</summary>
public sealed record ApiProblem(
    int Status,
    string Title,
    string Detail,
    string Code,
    string? CorrelationId,
    Dictionary<string, string[]>? Errors);

public static class ApiTestSupport
{
    public const string CorrelationHeader = "X-Correlation-ID";

    public static readonly JsonSerializerOptions Json = BuildOptions();

    private static long _sequence = 9_000_000_000;

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>A unique number, so every test visitor has its own mobile number (no accidental duplicates).</summary>
    public static long NextNumber() => Interlocked.Increment(ref _sequence);

    public static HttpClient NewClient(this ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),   // https, so the redirect middleware stays quiet
            AllowAutoRedirect = false
        });

    public static async Task<HttpClient> SignedInClientAsync(this ApiFactory factory, string username, string password)
    {
        var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password }, Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the demo account should be able to sign in");

        var login = await response.ReadAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return client;
    }

    public static Task<HttpClient> AdminAsync(this ApiFactory factory) =>
        factory.SignedInClientAsync("admin", "Admin@12345");

    public static Task<HttpClient> UserAsync(this ApiFactory factory) =>
        factory.SignedInClientAsync("user", "User@12345");

    public static Dictionary<string, object?> NewVisitorBody(Action<Dictionary<string, object?>>? change = null)
    {
        var number = NextNumber();
        var body = new Dictionary<string, object?>
        {
            ["visitorName"] = $"Test Visitor {number}",
            ["companyName"] = "Integration Co",
            ["mobileNumber"] = $"+91{number}",
            ["email"] = "visitor@example.test",
            ["visitDate"] = DateTime.UtcNow.AddDays(3),
            ["hostId"] = 1,
            ["purposeId"] = 1,
            ["remarks"] = "integration test"
        };
        change?.Invoke(body);
        return body;
    }

    public static async Task<VisitorDto> CreateVisitorAsync(this HttpClient admin, Dictionary<string, object?>? body = null)
    {
        var response = await admin.PostAsJsonAsync("/api/visitors", body ?? NewVisitorBody(), Json);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.ReadAsync<VisitorDto>();
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static Task<ApiProblem> ReadProblemAsync(this HttpResponseMessage response) =>
        response.ReadAsync<ApiProblem>();

    public static string CorrelationId(this HttpResponseMessage response) =>
        response.Headers.GetValues(CorrelationHeader).Single();
}