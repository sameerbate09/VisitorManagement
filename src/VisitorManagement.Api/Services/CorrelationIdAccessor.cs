using VisitorManagement.Api.Middleware;
using VisitorManagement.Application.Abstractions;

namespace VisitorManagement.Api.Services;

public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _accessor;

    public CorrelationIdAccessor(IHttpContextAccessor accessor) => _accessor = accessor;

    public string CorrelationId => _accessor.HttpContext?.GetCorrelationId() ?? "n/a";
}