using Microsoft.EntityFrameworkCore;
using VisitorManagement.Contracts;
using VisitorManagement.Domain.Entities;
using VisitorManagement.Infrastructure.Persistence;

namespace VisitorManagement.Infrastructure.Messaging;

public enum AuditProcessResult
{
    Recorded,
    Duplicate
}

public class VisitorCreatedAuditProcessor
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public VisitorCreatedAuditProcessor(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AuditProcessResult> ProcessAsync(VisitorCreatedMessage message, CancellationToken ct)
    {
        if (await _db.AuditLogs.AnyAsync(a => a.MessageId == message.MessageId, ct))
            return AuditProcessResult.Duplicate;

        _db.AuditLogs.Add(new AuditLog
        {
            CorrelationId = message.CorrelationId,
            MessageId = message.MessageId,
            EntityName = "Visitor",
            EntityId = message.VisitorId.ToString(),
            Action = "VisitorCreated",
            Source = LogSources.Function,
            Details = $"Visit scheduled for {message.VisitDateUtc:O} with host {message.HostId}; created by user {message.CreatedByUserId}.",
            CreatedAtUtc = _clock.GetUtcNow().UtcDateTime
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            return AuditProcessResult.Recorded;
        }
        catch (DbUpdateException)
        {
            // Another instance may have inserted the same MessageId first (unique index).
            _db.ChangeTracker.Clear();
            if (await _db.AuditLogs.AnyAsync(a => a.MessageId == message.MessageId, ct))
                return AuditProcessResult.Duplicate;

            throw;
        }
    }
}