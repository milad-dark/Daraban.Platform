using Daraban.Modules.Knowledge.Services.Dtos;
using FluentValidation;

namespace Daraban.Modules.Knowledge.Services.Validators;

// ---- Category validators -----------------------------------------------------------------

public class CreateKbCategoryRequestValidator : AbstractValidator<CreateKbCategoryRequest>
{
    public CreateKbCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(200).WithMessage("Category name must not exceed 200 characters.");

        RuleFor(x => x.Slug)
            .MaximumLength(200).WithMessage("Slug must not exceed 200 characters.")
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
                .WithMessage("Slug may only contain lowercase letters, digits, and single hyphens.")
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order must be non-negative.");
    }
}

public class UpdateKbCategoryRequestValidator : AbstractValidator<UpdateKbCategoryRequest>
{
    public UpdateKbCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(200).WithMessage("Category name must not exceed 200 characters.");

        RuleFor(x => x.Slug)
            .MaximumLength(200).WithMessage("Slug must not exceed 200 characters.")
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
                .WithMessage("Slug may only contain lowercase letters, digits, and single hyphens.")
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order must be non-negative.");
    }
}

// ---- Article validators ------------------------------------------------------------------

public class CreateKbArticleRequestValidator : AbstractValidator<CreateKbArticleRequest>
{
    public CreateKbArticleRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Article title is required.")
            .MaximumLength(500).WithMessage("Article title must not exceed 500 characters.");

        // The column is unbounded text; this ceiling exists to keep a runaway paste from
        // becoming a multi-megabyte row that the tsvector generator then has to chew through.
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Article content is required.")
            .MaximumLength(100_000).WithMessage("Article content must not exceed 100000 characters.");

        RuleFor(x => x.Summary)
            .MaximumLength(1000).WithMessage("Summary must not exceed 1000 characters.");

        RuleFor(x => x.Tags)
            .MaximumLength(500).WithMessage("Tags must not exceed 500 characters.");

        RuleForEach(x => x.Targets).SetValidator(new KbArticleTargetInputValidator());
    }
}

public class UpdateKbArticleRequestValidator : AbstractValidator<UpdateKbArticleRequest>
{
    public UpdateKbArticleRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Article title is required.")
            .MaximumLength(500).WithMessage("Article title must not exceed 500 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Article content is required.")
            .MaximumLength(100_000).WithMessage("Article content must not exceed 100000 characters.");

        RuleFor(x => x.Summary)
            .MaximumLength(1000).WithMessage("Summary must not exceed 1000 characters.");

        RuleFor(x => x.Tags)
            .MaximumLength(500).WithMessage("Tags must not exceed 500 characters.");

        RuleForEach(x => x.Targets).SetValidator(new KbArticleTargetInputValidator());
    }
}

public class KbArticleTargetInputValidator : AbstractValidator<KbArticleTargetInput>
{
    public KbArticleTargetInputValidator()
    {
        RuleFor(x => x.TargetType)
            .IsInEnum().WithMessage("Invalid target type.");

        // Only shape validation here. The pairing rules (All must have no id, everything else
        // must have one) live in KbArticleService.ValidateTargets so they apply to every caller,
        // not only ones that go through the pipeline validator.
        RuleFor(x => x.TargetId)
            .NotEqual(Guid.Empty).WithMessage("Target id must not be an empty GUID.")
            .When(x => x.TargetId.HasValue);
    }
}

public class ChangeKbArticleStatusRequestValidator : AbstractValidator<ChangeKbArticleStatusRequest>
{
    public ChangeKbArticleStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid article status.");
    }
}

// ---- Feedback + ticket-link validators ---------------------------------------------------

public class SubmitKbFeedbackRequestValidator : AbstractValidator<SubmitKbFeedbackRequest>
{
    public SubmitKbFeedbackRequestValidator()
    {
        RuleFor(x => x.Comment)
            .MaximumLength(2000).WithMessage("Comment must not exceed 2000 characters.");
    }
}

public class LinkKbArticleToTicketRequestValidator : AbstractValidator<LinkKbArticleToTicketRequest>
{
    public LinkKbArticleToTicketRequestValidator()
    {
        RuleFor(x => x.ArticleId)
            .NotEmpty().WithMessage("Article id is required.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
    }
}
