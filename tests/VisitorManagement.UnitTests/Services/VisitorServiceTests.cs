using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Application.Exceptions;
using VisitorManagement.Application.Services;
using VisitorManagement.Application.Validators;
using VisitorManagement.Contracts;
using VisitorManagement.Domain.Entities;
using VisitorManagement.Domain.Enums;
using VisitorManagement.UnitTests.Helpers;

namespace VisitorManagement.UnitTests.Services;

public class VisitorServiceTests
{
    private readonly IVisitorRepository _visitors = Substitute.For<IVisitorRepository>();
    private readonly IReferenceDataRepository _reference = Substitute.For<IReferenceDataRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IVisitorEventPublisher _publisher = Substitute.For<IVisitorEventPublisher>();
    private readonly ICorrelationIdAccessor _correlation = Substitute.For<ICorrelationIdAccessor>();
    private readonly FakeTimeProvider _clock = TestData.Clock();
    private readonly VisitorService _sut;

    public VisitorServiceTests()
    {
        _currentUser.UserId.Returns(7);
        _correlation.CorrelationId.Returns("corr-123");
        _reference.HostIsActiveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _reference.PurposeIsActiveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _visitors.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new VisitorDto { Id = call.Arg<int>() });

        _sut = new VisitorService(
            _visitors, _reference, _currentUser, _publisher, _correlation,
            new CreateVisitorRequestValidator(_clock), new UpdateVisitorRequestValidator(_clock),
            _clock, NullLogger<VisitorService>.Instance);
    }

    // ---------- Create ----------

    [Fact]
    public async Task CreateAsync_saves_the_visitor_and_publishes_one_event()
    {
        Visitor? saved = null;
        _ = _visitors.AddAsync(Arg.Do<Visitor>(v => { saved = v; v.Id = 42; }), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(TestData.CreateRequest(), CancellationToken.None);

        result.Id.Should().Be(42);
        saved.Should().NotBeNull();
        saved!.MobileNumber.Should().Be("+919876543210");
        saved.Status.Should().Be(VisitorStatus.Scheduled);
        saved.CreatedByUserId.Should().Be(7);
        saved.CreatedAtUtc.Should().Be(TestData.Now.UtcDateTime);

        await _visitors.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishVisitorCreatedAsync(
            Arg.Is<VisitorCreatedMessage>(m =>
                m.VisitorId == 42 &&
                m.MessageId == "visitor-created:42" &&
                m.CorrelationId == "corr-123" &&
                m.CreatedByUserId == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_trims_text_and_stores_empty_optional_fields_as_null()
    {
        Visitor? saved = null;
        _ = _visitors.AddAsync(Arg.Do<Visitor>(v => saved = v), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(
            TestData.CreateRequest(r =>
            {
                r.VisitorName = "  Rahul Verma  ";
                r.Email = "   ";
                r.Remarks = "";
            }),
            CancellationToken.None);

        saved!.VisitorName.Should().Be("Rahul Verma");
        saved.Email.Should().BeNull();
        saved.Remarks.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_visit_and_does_not_save_or_publish()
    {
        _visitors.ExistsDuplicateAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => _sut.CreateAsync(TestData.CreateRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already exists*");
        await _visitors.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisher.DidNotReceiveWithAnyArgs().PublishVisitorCreatedAsync(default!, default);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_inactive_host_with_a_field_error()
    {
        _reference.HostIsActiveAsync(1, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _sut.CreateAsync(TestData.CreateRequest(), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationFailedException>();
        thrown.Which.Errors.Should().ContainKey("hostId");
        await _visitors.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_rejects_invalid_input_before_touching_the_database()
    {
        var act = () => _sut.CreateAsync(
            TestData.CreateRequest(r => { r.VisitorName = ""; r.MobileNumber = "abc"; }),
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationFailedException>();
        thrown.Which.Errors.Should().ContainKeys("visitorName", "mobileNumber");
        await _visitors.DidNotReceive().AddAsync(Arg.Any<Visitor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_rejects_a_past_date()
    {
        var act = () => _sut.CreateAsync(
            TestData.CreateRequest(r => r.VisitDate = TestData.Now.UtcDateTime.AddDays(-1)),
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationFailedException>();
        thrown.Which.Errors.Should().ContainKey("visitDate");
    }

    [Fact]
    public async Task CreateAsync_still_succeeds_when_the_queue_is_unavailable()
    {
        // Failure case: the visitor is already saved, so a queue outage must not fail the request.
        _ = _visitors.AddAsync(Arg.Do<Visitor>(v => v.Id = 42), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _publisher.PublishVisitorCreatedAsync(Arg.Any<VisitorCreatedMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("queue down")));

        var result = await _sut.CreateAsync(TestData.CreateRequest(), CancellationToken.None);

        result.Id.Should().Be(42);
        await _visitors.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Update ----------

    [Fact]
    public async Task UpdateAsync_unknown_visitor_throws_not_found()
    {
        _visitors.GetEntityForUpdateAsync(99, Arg.Any<CancellationToken>()).Returns((Visitor?)null);

        var act = () => _sut.UpdateAsync(99, TestData.UpdateRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_applies_changes_and_records_who_and_when()
    {
        var existing = TestData.ExistingVisitor();
        _visitors.GetEntityForUpdateAsync(5, Arg.Any<CancellationToken>()).Returns(existing);
        var request = TestData.UpdateRequest(r => r.Status = VisitorStatus.CheckedIn);

        await _sut.UpdateAsync(5, request, CancellationToken.None);

        existing.Status.Should().Be(VisitorStatus.CheckedIn);
        existing.UpdatedByUserId.Should().Be(7);
        existing.UpdatedAtUtc.Should().Be(TestData.Now.UtcDateTime);
        _visitors.Received(1).SetExpectedRowVersion(existing, request.RowVersion);
        await _visitors.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_rejects_moving_a_visit_into_the_past()
    {
        _visitors.GetEntityForUpdateAsync(5, Arg.Any<CancellationToken>()).Returns(TestData.ExistingVisitor());
        var request = TestData.UpdateRequest(r => r.VisitDate = TestData.Now.UtcDateTime.AddDays(-2));

        var act = () => _sut.UpdateAsync(5, request, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationFailedException>();
        thrown.Which.Errors.Should().ContainKey("visitDate");
        await _visitors.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_does_not_recheck_an_unchanged_host_that_has_since_been_deactivated()
    {
        _visitors.GetEntityForUpdateAsync(5, Arg.Any<CancellationToken>()).Returns(TestData.ExistingVisitor());
        _reference.HostIsActiveAsync(1, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _sut.UpdateAsync(5, TestData.UpdateRequest(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_surfaces_a_stale_record_version_as_a_conflict()
    {
        // Failure case: someone else saved first.
        _visitors.GetEntityForUpdateAsync(5, Arg.Any<CancellationToken>()).Returns(TestData.ExistingVisitor());
        _visitors.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException("changed by someone else")));

        var act = () => _sut.UpdateAsync(5, TestData.UpdateRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*changed by someone else*");
    }

    [Fact]
    public async Task UpdateAsync_requires_a_row_version()
    {
        var act = () => _sut.UpdateAsync(
            5, TestData.UpdateRequest(r => r.RowVersion = Array.Empty<byte>()), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ValidationFailedException>();
        thrown.Which.Errors.Should().ContainKey("rowVersion");
    }

    // ---------- Get and list ----------

    [Fact]
    public async Task GetAsync_unknown_visitor_throws_not_found()
    {
        _visitors.GetByIdAsync(404, Arg.Any<CancellationToken>()).Returns((VisitorDto?)null);

        var act = () => _sut.GetAsync(404, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(0, 500, 1, 50)]    // page below 1 and page size above 50 are clamped
    [InlineData(-3, 0, 1, 1)]
    [InlineData(2, 20, 2, 20)]     // sensible values pass through
    public async Task ListAsync_clamps_paging_values(int page, int pageSize, int expectedPage, int expectedSize)
    {
        VisitorListQuery? captured = null;
        _visitors.SearchAsync(Arg.Do<VisitorListQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<VisitorDto>());

        await _sut.ListAsync(new VisitorListQuery { Page = page, PageSize = pageSize }, CancellationToken.None);

        captured!.Page.Should().Be(expectedPage);
        captured.PageSize.Should().Be(expectedSize);
    }

    [Fact]
    public async Task ListAsync_replaces_an_unknown_sort_field_and_trims_the_search_text()
    {
        VisitorListQuery? captured = null;
        _visitors.SearchAsync(Arg.Do<VisitorListQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<VisitorDto>());

        await _sut.ListAsync(
            new VisitorListQuery { SortBy = "name; DROP TABLE Visitors", Search = "  acme  " },
            CancellationToken.None);

        captured!.SortBy.Should().Be("visitDate");
        captured.Search.Should().Be("acme");
    }

    [Fact]
    public async Task ListAsync_defaults_to_newest_visits_first()
    {
        VisitorListQuery? captured = null;
        _visitors.SearchAsync(Arg.Do<VisitorListQuery>(q => captured = q), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<VisitorDto>());

        await _sut.ListAsync(new VisitorListQuery(), CancellationToken.None);

        captured!.SortBy.Should().Be("visitDate");
        captured.SortDirection.Should().Be("desc");
    }
}