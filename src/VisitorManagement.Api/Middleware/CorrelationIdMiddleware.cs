using System.Text.RegularExpressions;
using Serilog.Context;
using VisitorManagement.Contracts;

namespace VisitorManagement.Api.Middleware;

public class CorrelationIdMiddleware
{
    public const string ItemKey = "CorrelationId";

    // Only accept IDs that are safe to echo and log: no spaces, no control characters, bounded length.
    private static readonly Regex SafeId = new("^[A-Za-z0-9_-]{8,64}$", RegexOptions.Compiled);

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationConstants.HeaderName].FirstOrDefault();
        var id = incoming is not null && SafeId.IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = id;

        // OnStarting runs even when the exception handler has cleared the response headers.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationConstants.HeaderName] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationConstants.CorrelationIdProperty, id))
        {
            await _next(context);
        }
    }
}

public static class HttpContextCorrelationExtensions
{
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items[CorrelationIdMiddleware.ItemKey] as string ?? context.TraceIdentifier;
}