using VisitorManagement.Domain.Enums;

namespace VisitorManagement.Application.DTOs.Visitors;

public abstract class VisitorRequestBase
{
    public string VisitorName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime VisitDate { get; set; }
    public int HostId { get; set; }
    public int PurposeId { get; set; }
    public string? Remarks { get; set; }
}

public class CreateVisitorRequest : VisitorRequestBase { }

public class UpdateVisitorRequest : VisitorRequestBase
{
    public VisitorStatus Status { get; set; }

    /// <summary>Base64 in JSON. Must be the value returned by the last read.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class VisitorDto
{
    public int Id { get; set; }
    public string VisitorName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime VisitDate { get; set; }
    public int HostId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public int PurposeId { get; set; }
    public string PurposeName { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public VisitorStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class VisitorListQuery
{
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public static class VisitorSortFields
{
    public const string VisitorName = "visitorName";
    public const string CompanyName = "companyName";
    public const string HostName = "hostName";
    public const string VisitDate = "visitDate";
    public const string Status = "status";

    private static readonly string[] All = { VisitorName, CompanyName, HostName, VisitDate, Status };

    /// <summary>Whitelist: unknown values fall back to visitDate, so no raw input reaches the query.</summary>
    public static string Normalize(string? value) =>
        All.FirstOrDefault(f => string.Equals(f, value, StringComparison.OrdinalIgnoreCase)) ?? VisitDate;
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record HostDto(int Id, string FullName, string Department);
public record PurposeDto(int Id, string Name);