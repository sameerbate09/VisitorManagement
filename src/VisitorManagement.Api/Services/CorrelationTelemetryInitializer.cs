using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using VisitorManagement.Api.Middleware;
using VisitorManagement.Contracts;

namespace VisitorManagement.Api.Services;

public class CorrelationTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _accessor;

    public CorrelationTelemetryInitializer(IHttpContextAccessor accessor) => _accessor = accessor;

    public void Initialize(ITelemetry telemetry)
    {
        var context = _accessor.HttpContext;
        if (context is null || telemetry is not ISupportProperties properties) return;

        properties.Properties[CorrelationConstants.CorrelationIdProperty] = context.GetCorrelationId();
        properties.Properties[CorrelationConstants.SourceProperty] = LogSources.Api;
    }
}