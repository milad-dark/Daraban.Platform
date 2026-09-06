using FluentValidation;

namespace Daraban.Modules.Identity.Services.Users;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().MaximumLength(256)
            // Same charset rule as RegisterRequestValidator -- an admin-created account must not be
            // able to hold a username that self-registration would reject, or the two paths
            // disagree about what a valid login identifier is.
            .Matches("^[a-zA-Z0-9._-]+$")
                .WithMessage("Username may only contain letters, numbers, dots, underscores, and hyphens.");

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
    }
}
