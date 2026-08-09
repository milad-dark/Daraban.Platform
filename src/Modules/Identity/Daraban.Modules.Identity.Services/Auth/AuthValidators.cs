using FluentValidation;

namespace Daraban.Modules.Identity.Services.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    // A minimal floor, not a full corpus -- a real deployment should check candidate
    // passwords against a breach list (e.g. Have I Been Pwned's k-anonymity range API,
    // which never sends the actual password) rather than rely on a hardcoded list like
    // this one. Left as a documented gap, not silently skipped.
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password123", "12345678", "123456789", "qwerty123",
        "letmein", "welcome1", "admin123", "changeme", "iloveyou",
    };

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().MaximumLength(256)
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Username may only contain letters, numbers, dots, underscores, and hyphens.");

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);

        // Length over forced complexity rules -- current guidance (NIST SP 800-63B, OWASP)
        // favors a reasonable minimum length plus a breach/common-password check over
        // "must contain 1 uppercase, 1 digit, 1 symbol" rules, which push users toward
        // predictable substitutions (Password1!) without meaningfully raising entropy.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12).WithMessage("Password must be at least 12 characters.")
            .MaximumLength(128) // upper bound so an attacker can't force expensive PBKDF2 work on a huge input
            .Must(p => !CommonPasswords.Contains(p)).WithMessage("That password is too common -- please choose another.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
