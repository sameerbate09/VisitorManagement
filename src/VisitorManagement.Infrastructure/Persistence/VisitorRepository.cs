using Microsoft.EntityFrameworkCore;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Application.Exceptions;
using VisitorManagement.Domain.Entities;
using VisitorManagement.Domain.Enums;

namespace VisitorManagement.Infrastructure.Persistence;

public class VisitorRepository : IVisitorRepository
{
    private readonly AppDbContext _db;

    public VisitorRepository(AppDbContext db) => _db = db;

    public async Task<VisitorDto?> GetByIdAsync(int id, CancellationToken ct) =>
        await Project(_db.Visitors.AsNoTracking().Where(v => v.Id == id)).FirstOrDefaultAsync(ct);

    public async Task<Visitor?> GetEntityForUpdateAsync(int id, CancellationToken ct) =>
        await _db.Visitors.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<PagedResult<VisitorDto>> SearchAsync(VisitorListQuery query, CancellationToken ct)
    {
        IQueryable<Visitor> source = _db.Visitors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search;   // passed to SQL as a parameter, never concatenated
            source = source.Where(v =>
                v.VisitorName.Contains(term) ||
                v.CompanyName.Contains(term) ||
                v.Host.FullName.Contains(term) ||
                v.MobileNumber.Contains(term));
        }

        var total = await source.CountAsync(ct);

        var items = await Project(
                ApplySort(source, query)
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize))
            .ToListAsync(ct);

        return new PagedResult<VisitorDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<bool> ExistsDuplicateAsync(
        string mobile, int hostId, DateTime visitDateUtc, int? excludeId, CancellationToken ct)
    {
        var dayStart = visitDateUtc.Date;
        var dayEnd = dayStart.AddDays(1);

        return await _db.Visitors.AnyAsync(v =>
            v.MobileNumber == mobile &&
            v.HostId == hostId &&
            v.Status != VisitorStatus.Cancelled &&
            v.VisitDate >= dayStart && v.VisitDate < dayEnd &&
            (excludeId == null || v.Id != excludeId), ct);
    }

    public async Task AddAsync(Visitor visitor, CancellationToken ct) =>
        await _db.Visitors.AddAsync(visitor, ct);

    public void SetExpectedRowVersion(Visitor visitor, byte[] rowVersion) =>
        _db.Entry(visitor).Property(v => v.RowVersion).OriginalValue = rowVersion;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "This visitor record was changed by someone else. Reload it and try again.");
        }
    }

    private static IQueryable<Visitor> ApplySort(IQueryable<Visitor> source, VisitorListQuery query)
    {
        var desc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<Visitor> ordered = query.SortBy switch
        {
            VisitorSortFields.VisitorName => desc ? source.OrderByDescending(v => v.VisitorName) : source.OrderBy(v => v.VisitorName),
            VisitorSortFields.CompanyName => desc ? source.OrderByDescending(v => v.CompanyName) : source.OrderBy(v => v.CompanyName),
            VisitorSortFields.HostName => desc ? source.OrderByDescending(v => v.Host.FullName) : source.OrderBy(v => v.Host.FullName),
            VisitorSortFields.Status => desc ? source.OrderByDescending(v => v.Status) : source.OrderBy(v => v.Status),
            _ => desc ? source.OrderByDescending(v => v.VisitDate) : source.OrderBy(v => v.VisitDate)
        };

        return ordered.ThenBy(v => v.Id);   // stable order, so paging never repeats or skips rows
    }

    private static IQueryable<VisitorDto> Project(IQueryable<Visitor> source) =>
        source.Select(v => new VisitorDto
        {
            Id = v.Id,
            VisitorName = v.VisitorName,
            CompanyName = v.CompanyName,
            MobileNumber = v.MobileNumber,
            Email = v.Email,
            VisitDate = v.VisitDate,
            HostId = v.HostId,
            HostName = v.Host.FullName,
            PurposeId = v.PurposeId,
            PurposeName = v.Purpose.Name,
            Remarks = v.Remarks,
            Status = v.Status,
            CreatedAtUtc = v.CreatedAtUtc,
            UpdatedAtUtc = v.UpdatedAtUtc,
            RowVersion = v.RowVersion
        });
}