using FluentValidation;

namespace Daraban.Modules.Discovery.Services.Validators;

/// <summary>Validator for CreateRangeRequest (Task 5.1).</summary>
public class CreateRangeRequestValidator : AbstractValidator<CreateRangeRequest>
{
    public CreateRangeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Range name is required.")
            .MaximumLength(200).WithMessage("Range name must not exceed 200 characters.");

        RuleFor(x => x.CidrRange)
            .NotEmpty().WithMessage("CIDR range is required.")
            .MaximumLength(50).WithMessage("CIDR range must not exceed 50 characters.")
            .Matches(@"^(\d{1,3}\.){3}\d{1,3}/\d{1,2}$").WithMessage("Invalid CIDR format (e.g., 192.168.1.0/24).");

        RuleFor(x => x.StartIp)
            .MaximumLength(45).WithMessage("Start IP must not exceed 45 characters.")
            .Matches(@"^(\d{1,3}\.){3}\d{1,3}$").WithMessage("Invalid IP address format.")
            .When(x => !string.IsNullOrEmpty(x.StartIp));

        RuleFor(x => x.EndIp)
            .MaximumLength(45).WithMessage("End IP must not exceed 45 characters.")
            .Matches(@"^(\d{1,3}\.){3}\d{1,3}$").WithMessage("Invalid IP address format.")
            .When(x => !string.IsNullOrEmpty(x.EndIp));

        RuleFor(x => x.ScanIntervalHours)
            .GreaterThanOrEqualTo(0).WithMessage("Scan interval must be non-negative.")
            .LessThanOrEqualTo(8760).WithMessage("Scan interval must not exceed 1 year (8760 hours).");
    }
}
