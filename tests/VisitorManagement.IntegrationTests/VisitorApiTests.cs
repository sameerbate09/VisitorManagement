using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Domain.Enums;
using VisitorManagement.IntegrationTests.Support;

namespace VisitorManagement.IntegrationTests;

[Collection(ApiCollection.Name)]
public class VisitorApiTests(ApiFactory factory)
{
    // ---------- Create ----------

    [Fact]
    public async Task Admin_creates_a_visitor_and_one_event_is_published_with_the_same_correlation_id()
    {
        var admin = await factory.AdminAsync();
        var number = ApiTestSupport.NextNumber();
        var body = ApiTestSupport.NewVisitorBody(b =>
            b["mobileNumber"] = $"+91 {number / 100_000} {number % 100_000:D5}");   // typed with spaces

        var response = await admin.PostAsJsonAsync("/api/visitors", body, ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadAsync<VisitorDto>();
        created.Id.Should().BeGreaterThan(0);
        created.MobileNumber.Should().Be($"+91{number}", "the API stores a normalized number");
        created.Status.Should().Be(VisitorStatus.Scheduled);
        created.HostName.Should().NotBeNullOrWhiteSpace();
        response.Headers.Location!.ToString().Should().EndWith($"/api/visitors/{created.Id}");

        var message = factory.Publisher.Messages.Single(m => m.VisitorId == created.Id);
        message.MessageId.Should().Be($"visitor-created:{created.Id}");
        message.CorrelationId.Should().Be(response.CorrelationId(), "one ID follows the request into the queue message");
    }

    [Fact]
    public async Task A_caller_supplied_correlation_id_is_echoed_and_reaches_the_queue_message()
    {
        var admin = await factory.AdminAsync();
        admin.DefaultRequestHeaders.Add(ApiTestSupport.CorrelationHeader, "int-test-trace-0001");

        var response = await admin.PostAsJsonAsync("/api/visitors", ApiTestSupport.NewVisitorBody(), ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.CorrelationId().Should().Be("int-test-trace-0001");
        var created = await response.ReadAsync<VisitorDto>();
        factory.Publisher.Messages.Single(m => m.VisitorId == created.Id).CorrelationId
            .Should().Be("int-test-trace-0001");
    }

    [Fact]
    public async Task A_user_with_the_User_role_cannot_create_a_visitor()
    {
        var user = await factory.UserAsync();
        var before = factory.Publisher.Messages.Count;

        var response = await user.PostAsJsonAsync("/api/visitors", ApiTestSupport.NewVisitorBody(), ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.ReadProblemAsync()).Code.Should().Be("forbidden");
        factory.Publisher.Messages.Count.Should().Be(before);
    }

    [Fact]
    public async Task A_user_with_the_User_role_cannot_update_a_visitor()
    {
        var user = await factory.UserAsync();

        var response = await user.PutAsJsonAsync("/api/visitors/1", ApiTestSupport.NewVisitorBody(), ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_duplicate_visit_returns_409_and_publishes_only_once()
    {
        var admin = await factory.AdminAsync();
        var before = factory.Publisher.Messages.Count;
        var body = ApiTestSupport.NewVisitorBody();

        var first = await admin.PostAsJsonAsync("/api/visitors", body, ApiTestSupport.Json);
        var second = await admin.PostAsJsonAsync("/api/visitors", body, ApiTestSupport.Json);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.ReadProblemAsync()).Code.Should().Be("conflict");
        factory.Publisher.Messages.Count.Should().Be(before + 1);
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_field_errors_and_publishes_nothing()
    {
        var admin = await factory.AdminAsync();
        var before = factory.Publisher.Messages.Count;
        var body = ApiTestSupport.NewVisitorBody(b =>
        {
            b["visitorName"] = "";
            b["mobileNumber"] = "abc";
        });

        var response = await admin.PostAsJsonAsync("/api/visitors", body, ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadProblemAsync();
        problem.Code.Should().Be("validation_failed");
        problem.Errors.Should().ContainKeys("visitorName", "mobileNumber");
        problem.CorrelationId.Should().Be(response.CorrelationId());
        factory.Publisher.Messages.Count.Should().Be(before);
    }

    [Fact]
    public async Task A_past_visit_date_is_rejected()
    {
        var admin = await factory.AdminAsync();
        var body = ApiTestSupport.NewVisitorBody(b => b["visitDate"] = DateTime.UtcNow.AddDays(-2));

        var response = await admin.PostAsJsonAsync("/api/visitors", body, ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).Errors.Should().ContainKey("visitDate");
    }

    [Fact]
    public async Task An_unknown_host_is_rejected_with_a_field_error()
    {
        var admin = await factory.AdminAsync();
        var body = ApiTestSupport.NewVisitorBody(b => b["hostId"] = 9999);

        var response = await admin.PostAsJsonAsync("/api/visitors", body, ApiTestSupport.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadProblemAsync()).Errors.Should().ContainKey("hostId");
    }

    [Fact]
    public async Task A_queue_outage_does_not_fail_the_request_because_the_visitor_is_already_saved()
    {
        var admin = await factory.AdminAsync();
        factory.Publisher.FailWith = new InvalidOperationException("queue down");
        try
        {
            var response = await admin.PostAsJsonAsync("/api/visitors", ApiTestSupport.NewVisitorBody(), ApiTestSupport.Json);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await response.ReadAsync<VisitorDto>();
            (await admin.GetAsync($"/api/visitors/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            factory.Publisher.Messages.Should().NotContain(m => m.VisitorId == created.Id);
        }
        finally
        {
            factory.Publisher.FailWith = null;
        }
    }

    // ---------- Read ----------

    [Fact]
    public async Task Getting_an_unknown_visitor_returns_404()
    {
        var admin = await factory.AdminAsync();

        var response = await admin.GetAsync("/api/visitors/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadProblemAsync()).Code.Should().Be("not_found");
    }

    [Fact]
    public async Task A_user_with_the_User_role_can_view_the_list()
    {
        var user = await factory.UserAsync();

        var response = await user.GetAsync("/api/visitors?page=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<PagedResult<VisitorDto>>()).Page.Should().Be(1);
    }

    [Fact]
    public async Task The_list_supports_search_sorting_and_server_side_paging()
    {
        var admin = await factory.AdminAsync();
        var company = $"Co-{Guid.NewGuid():N}"[..12];
        foreach (var name in new[] { "Charlie", "Alpha", "Bravo" })
        {
            await admin.CreateVisitorAsync(ApiTestSupport.NewVisitorBody(b =>
            {
                b["visitorName"] = name;
                b["companyName"] = company;
            }));
        }

        var search = Uri.EscapeDataString(company);
        var pageOne = await (await admin.GetAsync(
                $"/api/visitors?search={search}&sortBy=visitorName&sortDirection=asc&page=1&pageSize=2"))
            .ReadAsync<PagedResult<VisitorDto>>();
        var pageTwo = await (await admin.GetAsync(
                $"/api/visitors?search={search}&sortBy=visitorName&sortDirection=asc&page=2&pageSize=2"))
            .ReadAsync<PagedResult<VisitorDto>>();

        pageOne.TotalCount.Should().Be(3);
        pageOne.TotalPages.Should().Be(2);
        pageOne.Items.Select(v => v.VisitorName).Should().Equal("Alpha", "Bravo");
        pageTwo.Items.Select(v => v.VisitorName).Should().Equal("Charlie");
    }

    [Fact]
    public async Task Sorting_by_an_unknown_field_is_ignored_instead_of_failing()
    {
        var admin = await factory.AdminAsync();

        var response = await admin.GetAsync("/api/visitors?sortBy=name;DROP%20TABLE%20Visitors&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- Update ----------

    [Fact]
    public async Task Updating_with_a_stale_record_version_returns_409()
    {
        var admin = await factory.AdminAsync();
        var created = await admin.CreateVisitorAsync();

        object UpdateBody(VisitorStatus status) => new
        {
            visitorName = created.VisitorName,
            companyName = created.CompanyName,
            mobileNumber = created.MobileNumber,
            email = created.Email,
            visitDate = created.VisitDate,
            hostId = created.HostId,
            purposeId = created.PurposeId,
            remarks = created.Remarks,
            status,
            rowVersion = created.RowVersion
        };

        var first = await admin.PutAsJsonAsync($"/api/visitors/{created.Id}", UpdateBody(VisitorStatus.CheckedIn), ApiTestSupport.Json);
        var stale = await admin.PutAsJsonAsync($"/api/visitors/{created.Id}", UpdateBody(VisitorStatus.Completed), ApiTestSupport.Json);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await first.ReadAsync<VisitorDto>();
        updated.Status.Should().Be(VisitorStatus.CheckedIn);
        updated.RowVersion.Should().NotEqual(created.RowVersion);

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await stale.ReadProblemAsync();
        problem.Code.Should().Be("conflict");
        problem.Detail.Should().Contain("changed by someone else");
    }

    // ---------- Reference data ----------

    [Fact]
    public async Task Host_search_returns_matching_active_hosts_and_honours_the_limit()
    {
        var user = await factory.UserAsync();

        var all = await (await user.GetAsync("/api/hosts?search=an")).ReadAsync<List<HostDto>>();
        var limited = await (await user.GetAsync("/api/hosts?search=an&limit=2")).ReadAsync<List<HostDto>>();

        all.Should().NotBeEmpty().And.OnlyContain(h => h.FullName.Contains("an", StringComparison.OrdinalIgnoreCase));
        limited.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Purposes_returns_the_seeded_list()
    {
        var user = await factory.UserAsync();

        var purposes = await (await user.GetAsync("/api/purposes")).ReadAsync<List<PurposeDto>>();

        purposes.Should().HaveCount(7);
    }
}