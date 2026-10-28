using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using VisitorManagement.Contracts;
using VisitorManagement.Infrastructure.Messaging;

namespace VisitorManagement.Functions;

public class VisitorCreatedAuditFunction
{
    private const string Operation = "ProcessVisitorCreated";

    private readonly VisitorCreatedAuditProcessor _processor;
    private readonly ILogger<VisitorCreatedAuditFunction> _logger;

    public VisitorCreatedAuditFunction(
        VisitorCreatedAuditProcessor processor,
        ILogger<VisitorCreatedAuditFunction> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    [Function("VisitorCreatedAudit")]
    public async Task Run(
        [QueueTrigger(QueueNames.VisitorCreated, Connection = "AzureWebJobsStorage")] QueueMessage queueMessage,
        CancellationToken ct)
    {
        var message = Parse(queueMessage);

        // The scope puts CorrelationId and Source on every log line (and on Application Insights telemetry).
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationConstants.CorrelationIdProperty] = message.CorrelationId,
            [CorrelationConstants.SourceProperty] = LogSources.Function,
            ["Operation"] = Operation
        });

        _logger.LogInformation(
            "[{Source}] [{CorrelationId}] Operation={Operation} started: MessageId={MessageId}, Attempt={Attempt}",
            LogSources.Function, message.CorrelationId, Operation, message.MessageId, queueMessage.DequeueCount);

        try
        {
            var result = await _processor.ProcessAsync(message, ct);

            if (result == AuditProcessResult.Duplicate)
            {
                _logger.LogWarning(
                    "[{Source}] [{CorrelationId}] Operation={Operation} skipped duplicate message {MessageId}",
                    LogSources.Function, message.CorrelationId, Operation, message.MessageId);
                return;
            }

            _logger.LogInformation(
                "[{Source}] [{CorrelationId}] Operation={Operation} recorded audit entry for VisitorId={VisitorId}",
                LogSources.Function, message.CorrelationId, Operation, message.VisitorId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "[{Source}] [{CorrelationId}] Operation={Operation} failed on attempt {Attempt}",
                LogSources.Function, message.CorrelationId, Operation, queueMessage.DequeueCount);
            throw;   // rethrow: the host retries, then moves the message to the poison queue
        }
    }

    private VisitorCreatedMessage Parse(QueueMessage queueMessage)
    {
        VisitorCreatedMessage? message = null;

        try
        {
            message = JsonSerializer.Deserialize<VisitorCreatedMessage>(
                queueMessage.Body.ToString(), MessageJson.Options);
        }
        catch (JsonException)
        {
            // handled below
        }

        if (message is null ||
            string.IsNullOrWhiteSpace(message.MessageId) ||
            string.IsNullOrWhiteSpace(message.CorrelationId) ||
            message.VisitorId <= 0)
        {
            _logger.LogError(
                "[{Source}] Operation={Operation} received an invalid queue message {QueueMessageId} (attempt {Attempt})",
                LogSources.Function, Operation, queueMessage.MessageId, queueMessage.DequeueCount);
            throw new InvalidOperationException("The queue message is invalid.");
        }

        return message;
    }
}