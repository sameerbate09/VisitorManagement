using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VisitorManagement.Api.Middleware;

namespace VisitorManagement.Api.ErrorHandling;

public static class ApiErrors
{
    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string title,
        string detail,
        string code,
        IDictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = errors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(errors);

        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = context.GetCorrelationId();
        return problem;
    }

    public static Task WriteAsync(HttpContext context, ProblemDetails problem, CancellationToken ct = default)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            (JsonSerializerOptions?)null,
            "application/problem+json",
            ct);
    }
}