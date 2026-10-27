using FluentValidation;
using VisitorManagement.Application.DTOs.Visitors;
using VisitorManagement.Application.Rules;

namespace VisitorManagement.Application.Validators;

public abstract class VisitorRequestValidatorBase<T> : AbstractValidator<T> where T : VisitorRequestBase
{
    protected VisitorRequestValidatorBase(TimeProvider clock)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;   // one message per field, the most relevant one

        RuleFor(x => x.VisitorName)
            .NotEmpty().WithMessage("Visitor name is required.")
            .MinimumLength(2).WithMessage("Visitor name must be at least 2 characters.")
            .MaximumLength(100).WithMessage("Visitor name must not exceed 100 characters.");

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(150).WithMessage("Company name must not exceed 150 characters.");

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("Mobile number is required.")
            .Must(VisitorRules.IsValidMobile)
            .WithMessage("Enter a valid mobile number: 7 to 15 digits, with an optional leading +.");

        RuleFor(x => x.Email)
            .MaximumLength(254).WithMessage("Email address must not exceed 254 characters.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.VisitDate)
            .NotEqual(default(DateTime)).WithMessage("Visit date is required.")
            .Must(d => VisitorRules.IsNotTooFarAhead(VisitorRules.ToUtc(d), clock.GetUtcNow().UtcDateTime))
            .WithMessage($"Visit date cannot be more than {VisitorRules.MaxFutureDays} days ahead.");

        RuleFor(x => x.HostId).GreaterThan(0).WithMessage("Host is required.");
        RuleFor(x => x.PurposeId).GreaterThan(0).WithMessage("Purpose of visit is required.");

        RuleFor(x => x.Remarks)
            .MaximumLength(500).WithMessage("Remarks must not exceed 500 characters.");
    }
}

public class CreateVisitorRequestValidator : VisitorRequestValidatorBase<CreateVisitorRequest>
{
    public CreateVisitorRequestValidator(TimeProvider clock) : base(clock)
    {
        RuleFor(x => x.VisitDate)
            .Must(d => VisitorRules.IsNotInPast(VisitorRules.ToUtc(d), clock.GetUtcNow().UtcDateTime))
            .WithMessage("Visit date cannot be in the past.")
            .When(x => x.VisitDate != default);
    }
}

public class UpdateVisitorRequestValidator : VisitorRequestValidatorBase<UpdateVisitorRequest>
{
    public UpdateVisitorRequestValidator(TimeProvider clock) : base(clock)
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage("Status is not valid.");
        RuleFor(x => x.RowVersion).NotEmpty()
            .WithMessage("The record version is required. Reload the visitor and try again.");
    }
}