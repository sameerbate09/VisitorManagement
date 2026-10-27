using VisitorManagement.Application.Abstractions;
using VisitorManagement.Application.DTOs.Visitors;

namespace VisitorManagement.Application.Services;

public class ReferenceDataService : IReferenceDataService
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 25;
    private const int MaxTermLength = 50;

    private readonly IReferenceDataRepository _reference;

    public ReferenceDataService(IReferenceDataRepository reference) => _reference = reference;

    public Task<IReadOnlyList<HostDto>> SearchHostsAsync(string? term, int? limit, CancellationToken ct)
    {
        var cleanTerm = string.IsNullOrWhiteSpace(term) ? null : term.Trim();
        if (cleanTerm is { Length: > MaxTermLength }) cleanTerm = cleanTerm[..MaxTermLength];

        return _reference.SearchHostsAsync(cleanTerm, Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit), ct);
    }

    public Task<IReadOnlyList<PurposeDto>> GetPurposesAsync(CancellationToken ct) =>
        _reference.GetPurposesAsync(ct);
}