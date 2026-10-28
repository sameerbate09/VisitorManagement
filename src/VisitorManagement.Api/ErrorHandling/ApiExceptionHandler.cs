using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using VisitorManagement.Api.Middleware;
using VisitorManagement.Application.Exceptions;

namespace VisitorManagement.Api.ErrorHandling;

public class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var operation = ResolveOperation(context);

        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Operation={Operation} cancelled because the client closed the request", operation);
            context.Response.StatusCode = 499;
            return true;
        }

        var correlationId = context.GetCorrelationId();

        (int Status, string Title, string Detail, string Code, IReadOnlyDictionary<string, string[]>? Errors) error =
            exception switch
            {
                ValidationFailedException v => (400, "Validation failed", "One or more fields are invalid.", "validation_failed", v.Errors),
                NotFoundException => (404, "Not found", "The requested record was not found.", "not_found", null),
                ConflictException c => (409, "Conflict", c.Message, "conflict", null),
                BadHttpRequestException => (400, "Bad request", "The request could not be understood.", "bad_request", null),
                _ => (500, "Unexpected error",
                                                $"Unable to complete the request. Please try again. Reference: {correlationId}",
                                                "unexpected_error", null)
            };

        if (error.Status >= 500)
        {
            // Technical detail goes to the server log only, never to the response.
            _logger.LogError(exception, "Operation={Operation} failed with an unhandled exception", operation);
        }
        else
        {
            _logger.LogWarning("Operation={Operation} rejected with {StatusCode} ({ErrorCode})",
                operation, error.Status, error.Code);
        }

        var problem = ApiErrors.Create(
            context, error.Status, error.Title, error.Detail, error.Code,
            error.Errors?.ToDictionary(e => e.Key, e => e.Value));

        await ApiErrors.WriteAsync(context, problem, ct);
        return true;
    }

    private static string ResolveOperation(HttpContext context)
    {
        var endpoint = context.Features.Get<IExceptionHandlerPathFeature>()?.Endpoint;
        var action = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        return action is null ? "Unknown" : $"{action.ControllerName}.{action.ActionName}";
    }
}