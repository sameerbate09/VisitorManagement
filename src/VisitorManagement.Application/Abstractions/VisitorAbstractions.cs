using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Domain.Entities;

namespace VisitorManagement.Application.Abstractions;

public interface ICurrentUser
{
    int UserId { get; }
}

public interface IVisitorRepository
{
    Task<VisitorDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<Visitor?> GetEntityForUpdateAsync(int id, CancellationToken ct);
    Task<PagedResult<VisitorDto>> SearchAsync(VisitorListQuery query, CancellationToken ct);
    Task<bool> ExistsDuplicateAsync(string mobile, int hostId, DateTime visitDateUtc, int? excludeId, CancellationToken ct);
    Task AddAsync(Visitor visitor, CancellationToken ct);
    void SetExpectedRowVersion(Visitor visitor, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IReferenceDataRepository
{
    Task<IReadOnlyList<HostDto>> SearchHostsAsync(string? term, int limit, CancellationToken ct);
    Task<IReadOnlyList<PurposeDto>> GetPurposesAsync(CancellationToken ct);
    Task<bool> HostIsActiveAsync(int id, CancellationToken ct);
    Task<bool> PurposeIsActiveAsync(int id, CancellationToken ct);
}

public interface IVisitorService
{
    Task<PagedResult<VisitorDto>> ListAsync(VisitorListQuery query, CancellationToken ct);
    Task<VisitorDto> GetAsync(int id, CancellationToken ct);
    Task<VisitorDto> CreateAsync(CreateVisitorRequest request, CancellationToken ct);
    Task<VisitorDto> UpdateAsync(int id, UpdateVisitorRequest request, CancellationToken ct);
}

public interface IReferenceDataService
{
    Task<IReadOnlyList<HostDto>> SearchHostsAsync(string? term, int? limit, CancellationToken ct);
    Task<IReadOnlyList<PurposeDto>> GetPurposesAsync(CancellationToken ct);
}