using VisitorManagement.Contracts;

namespace VisitorManagement.Application.Abstractions;

public interface IVisitorEventPublisher
{
    Task PublishVisitorCreatedAsync(VisitorCreatedMessage message, CancellationToken ct);
}