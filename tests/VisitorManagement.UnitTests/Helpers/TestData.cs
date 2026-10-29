using Microsoft.Extensions.Time.Testing;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Domain.Entities;
using VisitorManagement.Domain.Enums;

namespace VisitorManagement.UnitTests.Helpers;

public static class TestData
{
    /// <summary>A fixed "now" so date rules are deterministic.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    public static FakeTimeProvider Clock() => new(Now);

    public static CreateVisitorRequest CreateRequest(Action<CreateVisitorRequest>? change = null)
    {
        var request = new CreateVisitorRequest
        {
            VisitorName = "Rahul Verma",
            CompanyName = "Acme Supplies",
            MobileNumber = "+91 98765 43210",
            Email = "rahul@acme.example",
            VisitDate = Now.UtcDateTime.AddDays(2),
            HostId = 1,
            PurposeId = 1,
            Remarks = "Product demo"
        };
        change?.Invoke(request);
        return request;
    }

    public static UpdateVisitorRequest UpdateRequest(Action<UpdateVisitorRequest>? change = null)
    {
        var request = new UpdateVisitorRequest
        {
            VisitorName = "Rahul Verma",
            CompanyName = "Acme Supplies",
            MobileNumber = "+91 98765 43210",
            Email = "rahul@acme.example",
            VisitDate = Now.UtcDateTime.AddDays(5),   // same as ExistingVisitor, so unchanged by default
            HostId = 1,
            PurposeId = 1,
            Remarks = "Product demo",
            Status = VisitorStatus.Scheduled,
            RowVersion = new byte[] { 1, 2, 3, 4 }
        };
        change?.Invoke(request);
        return request;
    }

    public static Visitor ExistingVisitor() => new()
    {
        Id = 5,
        VisitorName = "Rahul Verma",
        CompanyName = "Acme Supplies",
        MobileNumber = "+919876543210",
        VisitDate = Now.UtcDateTime.AddDays(5),
        HostId = 1,
        PurposeId = 1,
        Status = VisitorStatus.Scheduled,
        CreatedByUserId = 1,
        CreatedAtUtc = Now.UtcDateTime,
        RowVersion = new byte[] { 1, 2, 3, 4 }
    };
}