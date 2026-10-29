using FluentAssertions;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Application.Validators;
using VisitorManagement.Domain.Enums;
using VisitorManagement.UnitTests.Helpers;

namespace VisitorManagement.UnitTests.Validators;

public class CreateVisitorRequestValidatorTests
{
    private readonly CreateVisitorRequestValidator _validator = new(TestData.Clock());
    private static readonly DateTime Now = TestData.Now.UtcDateTime;

    [Fact]
    public void Valid_request_passes() =>
        _validator.Validate(TestData.CreateRequest()).IsValid.Should().BeTrue();

    [Fact]
    public void Missing_required_fields_fail_on_each_field()
    {
        var request = TestData.CreateRequest(r =>
        {
            r.VisitorName = "";
            r.CompanyName = "";
            r.MobileNumber = "";
            r.HostId = 0;
            r.PurposeId = 0;
        });

        var result = _validator.Validate(request);

        result.Errors.Select(e => e.PropertyName).Should().Contain(new[]
        {
            nameof(CreateVisitorRequest.VisitorName),
            nameof(CreateVisitorRequest.CompanyName),
            nameof(CreateVisitorRequest.MobileNumber),
            nameof(CreateVisitorRequest.HostId),
            nameof(CreateVisitorRequest.PurposeId)
        });
    }

    [Fact]
    public void Single_character_name_fails() =>
        _validator.Validate(TestData.CreateRequest(r => r.VisitorName = "A"))
            .Errors.Should().Contain(e => e.PropertyName == nameof(CreateVisitorRequest.VisitorName));

    [Theory]
    [InlineData("abc")]
    [InlineData("123456")]
    [InlineData("+1234567890123456")]
    [InlineData("12-ab-56789")]
    public void Invalid_mobile_fails(string mobile) =>
        _validator.Validate(TestData.CreateRequest(r => r.MobileNumber = mobile))
            .Errors.Should().Contain(e => e.PropertyName == nameof(CreateVisitorRequest.MobileNumber));

    [Fact]
    public void Invalid_email_fails() =>
        _validator.Validate(TestData.CreateRequest(r => r.Email = "not-an-email"))
            .Errors.Should().Contain(e => e.PropertyName == nameof(CreateVisitorRequest.Email));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_email_is_allowed(string? email) =>
        _validator.Validate(TestData.CreateRequest(r => r.Email = email)).IsValid.Should().BeTrue();

    [Fact]
    public void Past_date_fails_with_clear_message()
    {
        var result = _validator.Validate(TestData.CreateRequest(r => r.VisitDate = Now.AddDays(-1)));

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateVisitorRequest.VisitDate) &&
            e.ErrorMessage == "Visit date cannot be in the past.");
    }

    [Fact]
    public void Date_within_the_grace_window_passes() =>
        _validator.Validate(TestData.CreateRequest(r => r.VisitDate = Now.AddMinutes(-10))).IsValid.Should().BeTrue();

    [Fact]
    public void Date_just_outside_the_grace_window_fails() =>
        _validator.Validate(TestData.CreateRequest(r => r.VisitDate = Now.AddMinutes(-20)))
            .Errors.Should().Contain(e => e.PropertyName == nameof(CreateVisitorRequest.VisitDate));

    [Fact]
    public void Date_more_than_a_year_ahead_fails() =>
        _validator.Validate(TestData.CreateRequest(r => r.VisitDate = Now.AddDays(366)))
            .Errors.Should().Contain(e =>
                e.PropertyName == nameof(CreateVisitorRequest.VisitDate) && e.ErrorMessage.Contains("365"));

    [Fact]
    public void Date_exactly_a_year_ahead_passes() =>
        _validator.Validate(TestData.CreateRequest(r => r.VisitDate = Now.AddDays(365))).IsValid.Should().BeTrue();

    [Fact]
    public void Missing_date_fails() =>
        _validator.Validate(TestData.CreateRequest(r => r.VisitDate = default))
            .Errors.Should().Contain(e => e.PropertyName == nameof(CreateVisitorRequest.VisitDate));

    [Fact]
    public void Remarks_over_500_characters_fail() =>
        _validator.Validate(TestData.CreateRequest(r => r.Remarks = new string('x', 501)))
            .Errors.Should().Contain(e => e.PropertyName == nameof(CreateVisitorRequest.Remarks));
}

public class UpdateVisitorRequestValidatorTests
{
    private readonly UpdateVisitorRequestValidator _validator = new(TestData.Clock());

    [Fact]
    public void Valid_request_passes() =>
        _validator.Validate(TestData.UpdateRequest()).IsValid.Should().BeTrue();

    [Fact]
    public void Missing_row_version_fails() =>
        _validator.Validate(TestData.UpdateRequest(r => r.RowVersion = Array.Empty<byte>()))
            .Errors.Should().Contain(e => e.PropertyName == nameof(UpdateVisitorRequest.RowVersion));

    [Fact]
    public void Unknown_status_fails() =>
        _validator.Validate(TestData.UpdateRequest(r => r.Status = (VisitorStatus)99))
            .Errors.Should().Contain(e => e.PropertyName == nameof(UpdateVisitorRequest.Status));

    [Fact]
    public void Past_date_is_not_rejected_by_the_validator_itself() =>
        // The "do not move into the past" rule is applied in the service, so old records stay editable.
        _validator.Validate(TestData.UpdateRequest(r => r.VisitDate = TestData.Now.UtcDateTime.AddDays(-30)))
            .IsValid.Should().BeTrue();
}