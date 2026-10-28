using FluentValidation;
using Microsoft.Extensions.Logging;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Application.Exceptions;
using VisitorManagement.Application.Rules;
using VisitorManagement.Contracts;
using VisitorManagement.Domain.Entities;
using VisitorManagement.Domain.Enums;

namespace VisitorManagement.Application.Services;

public class VisitorService : IVisitorService
{
    private const string DuplicateMessage =
        "A visit for this mobile number with the same host on this date already exists.";

    private readonly IVisitorRepository _visitors;
    private readonly IReferenceDataRepository _reference;
    private readonly ICurrentUser _currentUser;
    private readonly IVisitorEventPublisher _publisher;
    private readonly ICorrelationIdAccessor _correlation;
    private readonly IValidator<CreateVisitorRequest> _createValidator;
    private readonly IValidator<UpdateVisitorRequest> _updateValidator;
    private readonly TimeProvider _clock;
    private readonly ILogger<VisitorService> _logger;

    public VisitorService(
        IVisitorRepository visitors,
        IReferenceDataRepository reference,
        ICurrentUser currentUser,
        IVisitorEventPublisher publisher,
        ICorrelationIdAccessor correlation,
        IValidator<CreateVisitorRequest> createValidator,
        IValidator<UpdateVisitorRequest> updateValidator,
        TimeProvider clock,
        ILogger<VisitorService> logger)
    {
        _visitors = visitors;
        _reference = reference;
        _currentUser = currentUser;
        _publisher = publisher;
        _correlation = correlation;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _clock = clock;
        _logger = logger;
    }

    public Task<PagedResult<VisitorDto>> ListAsync(VisitorListQuery query, CancellationToken ct)
    {
        var sortBy = VisitorSortFields.Normalize(query.SortBy);
        var direction = query.SortDirection?.ToLowerInvariant() switch
        {
            "asc" => "asc",
            "desc" => "desc",
            _ => string.IsNullOrWhiteSpace(query.SortBy) ? "desc" : "asc"
        };

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (search is { Length: > 100 }) search = search[..100];

        var normalized = new VisitorListQuery
        {
            Search = search,
            SortBy = sortBy,
            SortDirection = direction,
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 50)
        };

        return _visitors.SearchAsync(normalized, ct);
    }

    public async Task<VisitorDto> GetAsync(int id, CancellationToken ct) =>
        await _visitors.GetByIdAsync(id, ct) ?? throw new NotFoundException("Visitor", id);

    public async Task<VisitorDto> CreateAsync(CreateVisitorRequest request, CancellationToken ct)
    {
        await ValidateAsync(_createValidator, request, "CreateVisitor", ct);
        await EnsureActiveReferencesAsync(request, existing: null, ct);

        var visitDateUtc = VisitorRules.ToUtc(request.VisitDate);
        var mobile = VisitorRules.NormalizeMobile(request.MobileNumber);

        if (await _visitors.ExistsDuplicateAsync(mobile, request.HostId, visitDateUtc, null, ct))
        {
            _logger.LogWarning("Operation={Operation} rejected: duplicate visit for host {HostId}",
                "CreateVisitor", request.HostId);
            throw new ConflictException(DuplicateMessage);
        }

        var visitor = new Visitor
        {
            Status = VisitorStatus.Scheduled,
            CreatedByUserId = _currentUser.UserId,
            CreatedAtUtc = _clock.GetUtcNow().UtcDateTime
        };
        Apply(visitor, request, mobile, visitDateUtc);

        await _visitors.AddAsync(visitor, ct);
        await _visitors.SaveChangesAsync(ct);

        _logger.LogInformation("Operation={Operation} succeeded: VisitorId={VisitorId} by user {UserId}",
            "CreateVisitor", visitor.Id, visitor.CreatedByUserId);

        await PublishVisitorCreatedAsync(visitor, ct);
        return await _visitors.GetByIdAsync(visitor.Id, ct)
            ?? throw new InvalidOperationException("Saved visitor could not be reloaded.");
    }

    public async Task<VisitorDto> UpdateAsync(int id, UpdateVisitorRequest request, CancellationToken ct)
    {
        await ValidateAsync(_updateValidator, request, "UpdateVisitor", ct);

        var visitor = await _visitors.GetEntityForUpdateAsync(id, ct)
            ?? throw new NotFoundException("Visitor", id);

        var now = _clock.GetUtcNow().UtcDateTime;
        var visitDateUtc = VisitorRules.ToUtc(request.VisitDate);

        if (visitDateUtc != visitor.VisitDate && !VisitorRules.IsNotInPast(visitDateUtc, now))
            throw new ValidationFailedException("visitDate", "Visit date cannot be moved to the past.");

        await EnsureActiveReferencesAsync(request, visitor, ct);

        var mobile = VisitorRules.NormalizeMobile(request.MobileNumber);
        if (request.Status != VisitorStatus.Cancelled &&
            await _visitors.ExistsDuplicateAsync(mobile, request.HostId, visitDateUtc, id, ct))
        {
            throw new ConflictException(DuplicateMessage);
        }

        Apply(visitor, request, mobile, visitDateUtc);
        visitor.Status = request.Status;
        visitor.UpdatedByUserId = _currentUser.UserId;
        visitor.UpdatedAtUtc = now;

        _visitors.SetExpectedRowVersion(visitor, request.RowVersion);
        await _visitors.SaveChangesAsync(ct);   // throws ConflictException on a stale RowVersion

        _logger.LogInformation("Operation={Operation} succeeded: VisitorId={VisitorId} by user {UserId}",
            "UpdateVisitor", id, visitor.UpdatedByUserId);

        return await _visitors.GetByIdAsync(id, ct) ?? throw new NotFoundException("Visitor", id);
    }

    private async Task PublishVisitorCreatedAsync(Visitor visitor, CancellationToken ct)
    {
        var message = new VisitorCreatedMessage(
            VisitorCreatedMessage.BuildMessageId(visitor.Id),
            _correlation.CorrelationId,
            visitor.Id,
            visitor.HostId,
            visitor.VisitDate,
            visitor.CreatedByUserId,
            _clock.GetUtcNow().UtcDateTime);

        try
        {
            await _publisher.PublishVisitorCreatedAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The visitor is already saved. Do not fail the request; log it for follow-up.
            _logger.LogError(ex,
                "Operation={Operation} visitor {VisitorId} was saved but the event could not be queued",
                "PublishVisitorCreated", visitor.Id);
        }
    }

    private static void Apply(Visitor visitor, VisitorRequestBase request, string mobile, DateTime visitDateUtc)
    {
        visitor.VisitorName = request.VisitorName.Trim();
        visitor.CompanyName = request.CompanyName.Trim();
        visitor.MobileNumber = mobile;
        visitor.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        visitor.VisitDate = visitDateUtc;
        visitor.HostId = request.HostId;
        visitor.PurposeId = request.PurposeId;
        visitor.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
    }

    private async Task EnsureActiveReferencesAsync(VisitorRequestBase request, Visitor? existing, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        // Only re-check references that changed, so editing an old record still works
        // after its host or purpose has been deactivated.
        if ((existing is null || existing.HostId != request.HostId) &&
            !await _reference.HostIsActiveAsync(request.HostId, ct))
            errors["hostId"] = new[] { "The selected host was not found or is inactive." };

        if ((existing is null || existing.PurposeId != request.PurposeId) &&
            !await _reference.PurposeIsActiveAsync(request.PurposeId, ct))
            errors["purposeId"] = new[] { "The selected purpose was not found or is inactive." };

        if (errors.Count > 0) throw new ValidationFailedException(errors);
    }

    private async Task ValidateAsync<T>(IValidator<T> validator, T instance, string operation, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (result.IsValid) return;

        var errors = result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        // Field names only. Submitted values (personal data) are never logged.
        _logger.LogWarning("Operation={Operation} validation failed for fields {Fields}",
            operation, string.Join(",", errors.Keys));

        throw new ValidationFailedException(errors);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}