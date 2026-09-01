using FluentValidation;

namespace Daraban.Modules.Discovery.Services.Validators;

/// <summary>Validator for CreateRuleRequest (Task 5.1).</summary>
public class CreateRuleRequestValidator : AbstractValidator<CreateRuleRequest>
{
    public CreateRuleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Rule name is required.")
            .MaximumLength(200).WithMessage("Rule name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.FilterCriteria)
            .NotEmpty().WithMessage("Filter criteria is required.")
            .Must(BeValidJson).WithMessage("Filter criteria must be valid JSON.");

        RuleFor(x => x.Action)
            .IsInEnum().WithMessage("Invalid match action.");

        RuleFor(x => x.AssetType)
            .MaximumLength(100).WithMessage("Asset type must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.AssetType));

        RuleFor(x => x.Tag)
            .MaximumLength(100).WithMessage("Tag must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Tag));

        RuleFor(x => x.Priority)
            .GreaterThanOrEqualTo(0).WithMessage("Priority must be non-negative.")
            .LessThanOrEqualTo(1000).WithMessage("Priority must not exceed 1000.");
    }

    private bool BeValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
