using System.Collections.Concurrent;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Contracts;

namespace VisitorManagement.IntegrationTests.Support;

/// <summary>Stands in for the Storage Queue: records messages and can simulate an outage.</summary>
public sealed class CapturingPublisher : IVisitorEventPublisher
{
    private readonly ConcurrentQueue<VisitorCreatedMessage> _messages = new();

    public Exception? FailWith { get; set; }

    public IReadOnlyList<VisitorCreatedMessage> Messages => _messages.ToArray();

    public Task PublishVisitorCreatedAsync(VisitorCreatedMessage message, CancellationToken ct)
    {
        if (FailWith is not null) return Task.FromException(FailWith);

        _messages.Enqueue(message);
        return Task.CompletedTask;
    }
}