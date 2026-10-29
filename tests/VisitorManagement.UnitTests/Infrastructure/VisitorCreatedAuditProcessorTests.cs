using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VisitorManagement.Contracts;
using VisitorManagement.Infrastructure.Messaging;
using VisitorManagement.Infrastructure.Persistence;
using VisitorManagement.UnitTests.Helpers;

namespace VisitorManagement.UnitTests.Infrastructure;

public sealed class VisitorCreatedAuditProcessorTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeTimeProvider _clock = TestData.Clock();

    public VisitorCreatedAuditProcessorTests()
    {
        _connection.Open();   // the in-memory database lives as long as this connection
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static VisitorCreatedMessage Message(int visitorId = 3, string correlationId = "corr-1") => new(
        VisitorCreatedMessage.BuildMessageId(visitorId),
        correlationId,
        visitorId,
        HostId: 2,
        VisitDateUtc: TestData.Now.UtcDateTime.AddDays(1),
        CreatedByUserId: 1,
        OccurredAtUtc: TestData.Now.UtcDateTime);

    /// <summary>Each call uses a fresh context, like separate Function invocations.</summary>
    private async Task<AuditProcessResult> ProcessAsync(VisitorCreatedMessage message)
    {
        await using var db = new AppDbContext(_options);
        return await new VisitorCreatedAuditProcessor(db, _clock).ProcessAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task A_new_message_records_one_audit_row_with_the_correlation_id()
    {
        var result = await ProcessAsync(Message());

        result.Should().Be(AuditProcessResult.Recorded);

        await using var db = new AppDbContext(_options);
        var row = await db.AuditLogs.SingleAsync();
        row.MessageId.Should().Be("visitor-created:3");
        row.CorrelationId.Should().Be("corr-1");
        row.EntityName.Should().Be("Visitor");
        row.EntityId.Should().Be("3");
        row.Action.Should().Be("VisitorCreated");
        row.Source.Should().Be("FUNCTION");
        row.CreatedAtUtc.Should().Be(TestData.Now.UtcDateTime);
    }

    [Fact]
    public async Task The_same_message_delivered_twice_is_recorded_only_once()
    {
        var first = await ProcessAsync(Message());
        var second = await ProcessAsync(Message());

        first.Should().Be(AuditProcessResult.Recorded);
        second.Should().Be(AuditProcessResult.Duplicate);

        await using var db = new AppDbContext(_options);
        (await db.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Different_visitors_each_get_their_own_audit_row()
    {
        await ProcessAsync(Message(visitorId: 3));
        await ProcessAsync(Message(visitorId: 4));

        await using var db = new AppDbContext(_options);
        (await db.AuditLogs.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Audit_details_hold_identifiers_only_no_personal_data()
    {
        await ProcessAsync(Message());

        await using var db = new AppDbContext(_options);
        var details = (await db.AuditLogs.SingleAsync()).Details;
        details.Should().Contain("host 2").And.Contain("user 1");
    }

    [Fact]
    public async Task The_database_itself_rejects_a_duplicate_message_id()
    {
        // The safety net behind the application check: the unique index.
        await ProcessAsync(Message());

        await using var db = new AppDbContext(_options);
        db.AuditLogs.Add(new VisitorManagement.Domain.Entities.AuditLog
        {
            CorrelationId = "corr-x",
            MessageId = "visitor-created:3",
            EntityName = "Visitor",
            EntityId = "3",
            Action = "VisitorCreated",
            Source = "FUNCTION",
            CreatedAtUtc = TestData.Now.UtcDateTime
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}