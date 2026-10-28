using System.Text.Json;

namespace VisitorManagement.Contracts;

public static class QueueNames
{
    public const string VisitorCreated = "visitor-created";   // lowercase: Azure queue naming rule
}

public record VisitorCreatedMessage(
    string MessageId,
    string CorrelationId,
    int VisitorId,
    int HostId,
    DateTime VisitDateUtc,
    int CreatedByUserId,
    DateTime OccurredAtUtc)
{
    /// <summary>Deterministic key: the same visitor always produces the same id, so duplicates are harmless.</summary>
    public static string BuildMessageId(int visitorId) => $"visitor-created:{visitorId}";
}

public static class MessageJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}