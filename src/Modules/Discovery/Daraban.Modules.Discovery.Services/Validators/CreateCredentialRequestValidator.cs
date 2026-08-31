using FluentValidation;

namespace Daraban.Modules.Discovery.Services.Validators;

/// <summary>Validator for CreateCredentialRequest (Task 5.1).</summary>
public class CreateCredentialRequestValidator : AbstractValidator<CreateCredentialRequest>
{
    public CreateCredentialRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Credential name is required.")
            .MaximumLength(200).WithMessage("Credential name must not exceed 200 characters.");

        RuleFor(x => x.Version)
            .IsInEnum().WithMessage("Invalid SNMP version.");

        // SNMPv1/v2c validation
        RuleFor(x => x.CommunityString)
            .NotEmpty().WithMessage("Community string is required for SNMPv1/v2c.")
            .MaximumLength(200).WithMessage("Community string must not exceed 200 characters.")
            .When(x => x.Version == Data.Entities.SnmpVersion.V1 || x.Version == Data.Entities.SnmpVersion.V2c);

        // SNMPv3 validation
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required for SNMPv3.")
            .MaximumLength(100).WithMessage("Username must not exceed 100 characters.")
            .When(x => x.Version == Data.Entities.SnmpVersion.V3);

        RuleFor(x => x.AuthPassphrase)
            .NotEmpty().WithMessage("Auth passphrase is required when auth protocol is selected.")
            .MinimumLength(8).WithMessage("Auth passphrase must be at least 8 characters.")
            .When(x => x.Version == Data.Entities.SnmpVersion.V3 && x.AuthProtocol != Data.Entities.AuthProtocol.None);

        RuleFor(x => x.PrivPassphrase)
            .NotEmpty().WithMessage("Priv passphrase is required when privacy protocol is selected.")
            .MinimumLength(8).WithMessage("Priv passphrase must be at least 8 characters.")
            .When(x => x.Version == Data.Entities.SnmpVersion.V3 && x.PrivProtocol != Data.Entities.PrivProtocol.None);
    }
}
