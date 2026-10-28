using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Contracts;

namespace VisitorManagement.Infrastructure.Messaging;

public class QueueVisitorEventPublisher : IVisitorEventPublisher
{
    private static volatile bool _queueEnsured;

    private readonly QueueClient _queue;
    private readonly ILogger<QueueVisitorEventPublisher> _logger;

    public QueueVisitorEventPublisher(QueueClient queue, ILogger<QueueVisitorEventPublisher> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public async Task PublishVisitorCreatedAsync(VisitorCreatedMessage message, CancellationToken ct)
    {
        if (!_queueEnsured)
        {
            await _queue.CreateIfNotExistsAsync(cancellationToken: ct);
            _queueEnsured = true;
        }

        var json = JsonSerializer.Serialize(message, MessageJson.Options);
        await _queue.SendMessageAsync(json, cancellationToken: ct);

        _logger.LogInformation("Operation={Operation} queued event {MessageId}",
            "PublishVisitorCreated", message.MessageId);
    }
}