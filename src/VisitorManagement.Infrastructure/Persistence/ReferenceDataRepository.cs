using Microsoft.EntityFrameworkCore;
using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.Infrastructure.Persistence;

public class ReferenceDataRepository : IReferenceDataRepository
{
    private readonly AppDbContext _db;

    public ReferenceDataRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<HostDto>> SearchHostsAsync(string? term, int limit, CancellationToken ct)
    {
        var hosts = _db.Hosts.AsNoTracking().Where(h => h.IsActive);
        IOrderedQueryable<Host> ordered;

        if (string.IsNullOrWhiteSpace(term))
        {
            ordered = hosts.OrderBy(h => h.FullName);
        }
        else
        {
            hosts = hosts.Where(h => h.FullName.Contains(term));
            // Names that start with the typed text come first.
            ordered = hosts.OrderBy(h => h.FullName.StartsWith(term) ? 0 : 1).ThenBy(h => h.FullName);
        }

        return await ordered
            .Take(limit)
            .Select(h => new HostDto(h.Id, h.FullName, h.Department))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PurposeDto>> GetPurposesAsync(CancellationToken ct) =>
        await _db.Purposes.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new PurposeDto(p.Id, p.Name))
            .ToListAsync(ct);

    public Task<bool> HostIsActiveAsync(int id, CancellationToken ct) =>
        _db.Hosts.AnyAsync(h => h.Id == id && h.IsActive, ct);

    public Task<bool> PurposeIsActiveAsync(int id, CancellationToken ct) =>
        _db.Purposes.AnyAsync(p => p.Id == id && p.IsActive, ct);
}