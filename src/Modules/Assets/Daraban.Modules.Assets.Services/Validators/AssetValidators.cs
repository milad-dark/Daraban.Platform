using Daraban.Modules.Assets.Services.Dtos;
using FluentValidation;

namespace Daraban.Modules.Assets.Services.Validators;

public class CreateAssetRequestValidator : AbstractValidator<CreateAssetRequest>
{
    public CreateAssetRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Asset name is required.")
            .MaximumLength(300).WithMessage("Asset name must not exceed 300 characters.");

        RuleFor(x => x.AssetTypeId)
            .NotEmpty().WithMessage("Asset type is required.");

        RuleFor(x => x.EntityNodeId)
            .NotEmpty().WithMessage("Entity scope is required.");

        RuleFor(x => x.AssetTag)
            .MaximumLength(100).WithMessage("Asset tag must not exceed 100 characters.");

        RuleFor(x => x.SerialNumber)
            .MaximumLength(200).WithMessage("Serial number must not exceed 200 characters.");

        RuleFor(x => x.PurchaseCurrency)
            .MaximumLength(3).WithMessage("Currency code must not exceed 3 characters.");

        RuleFor(x => x.OrderNumber)
            .MaximumLength(100).WithMessage("Order number must not exceed 100 characters.");

        RuleFor(x => x.SupplierName)
            .MaximumLength(200).WithMessage("Supplier name must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.");
    }
}

public class UpdateAssetRequestValidator : AbstractValidator<UpdateAssetRequest>
{
    public UpdateAssetRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Asset name is required.")
            .MaximumLength(300).WithMessage("Asset name must not exceed 300 characters.");

        RuleFor(x => x.AssetTag)
            .MaximumLength(100).WithMessage("Asset tag must not exceed 100 characters.");

        RuleFor(x => x.SerialNumber)
            .MaximumLength(200).WithMessage("Serial number must not exceed 200 characters.");

        RuleFor(x => x.PurchaseCurrency)
            .MaximumLength(3).WithMessage("Currency code must not exceed 3 characters.");

        RuleFor(x => x.OrderNumber)
            .MaximumLength(100).WithMessage("Order number must not exceed 100 characters.");

        RuleFor(x => x.SupplierName)
            .MaximumLength(200).WithMessage("Supplier name must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.");
    }
}

public class CreateAssetTypeRequestValidator : AbstractValidator<CreateAssetTypeRequest>
{
    public CreateAssetTypeRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Asset type name is required.")
            .MaximumLength(200).WithMessage("Asset type name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.Icon)
            .MaximumLength(100).WithMessage("Icon must not exceed 100 characters.");
    }
}

public class CreateAssetCategoryRequestValidator : AbstractValidator<CreateAssetCategoryRequest>
{
    public CreateAssetCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(200).WithMessage("Category name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");
    }
}

public class CreateLocationRequestValidator : AbstractValidator<CreateLocationRequest>
{
    public CreateLocationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Location name is required.")
            .MaximumLength(200).WithMessage("Location name must not exceed 200 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.");

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code must not exceed 20 characters.");

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City must not exceed 100 characters.");

        RuleFor(x => x.Country)
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters.");
    }
}

public class CreateManufacturerRequestValidator : AbstractValidator<CreateManufacturerRequest>
{
    public CreateManufacturerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Manufacturer name is required.")
            .MaximumLength(200).WithMessage("Manufacturer name must not exceed 200 characters.");

        RuleFor(x => x.Website)
            .MaximumLength(500).WithMessage("Website must not exceed 500 characters.");

        RuleFor(x => x.SupportUrl)
            .MaximumLength(500).WithMessage("Support URL must not exceed 500 characters.");

        RuleFor(x => x.SupportPhone)
            .MaximumLength(50).WithMessage("Support phone must not exceed 50 characters.");
    }
}

public class AssignAssetRequestValidator : AbstractValidator<AssignAssetRequest>
{
    public AssignAssetRequestValidator()
    {
        RuleFor(x => x.TargetId)
            .NotEmpty().WithMessage("Assignment target is required.");

        RuleFor(x => x.TargetName)
            .MaximumLength(300).WithMessage("Target name must not exceed 300 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes must not exceed 1000 characters.");
    }
}

public class LifecycleTransitionRequestValidator : AbstractValidator<LifecycleTransitionRequest>
{
    public LifecycleTransitionRequestValidator()
    {
        // Reason is required only for Retire and Dispose (per roadmap Task 3.4).
        // Other transitions (Archive, Restore, Maintain) accept optional reason.
        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.");
    }
}
