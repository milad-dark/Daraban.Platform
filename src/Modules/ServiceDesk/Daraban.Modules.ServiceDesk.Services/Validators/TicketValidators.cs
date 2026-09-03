using Daraban.Modules.ServiceDesk.Services.Dtos;
using FluentValidation;

namespace Daraban.Modules.ServiceDesk.Services.Validators;

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ticket title is required.")
            .MaximumLength(500).WithMessage("Ticket title must not exceed 500 characters.");

        RuleFor(x => x.RequesterUserId)
            .NotEmpty().WithMessage("Requester user is required.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Description must not exceed 10000 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid ticket type.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid ticket priority.");

        RuleFor(x => x.Impact)
            .IsInEnum().WithMessage("Invalid ticket impact.");

        RuleFor(x => x.Urgency)
            .IsInEnum().WithMessage("Invalid ticket urgency.");

        RuleFor(x => x.Source)
            .IsInEnum().WithMessage("Invalid ticket source.");
    }
}

public class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ticket title is required.")
            .MaximumLength(500).WithMessage("Ticket title must not exceed 500 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Description must not exceed 10000 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid ticket type.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid ticket priority.");

        RuleFor(x => x.Impact)
            .IsInEnum().WithMessage("Invalid ticket impact.");

        RuleFor(x => x.Urgency)
            .IsInEnum().WithMessage("Invalid ticket urgency.");
    }
}

public class CreateTicketTaskRequestValidator : AbstractValidator<CreateTicketTaskRequest>
{
    public CreateTicketTaskRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Task content is required.")
            .MaximumLength(10000).WithMessage("Task content must not exceed 10000 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid task type.");

        RuleFor(x => x.TimeSpentMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("Time spent must be non-negative.")
            .LessThanOrEqualTo(1440).WithMessage("Time spent must not exceed 24 hours (1440 minutes).");
    }
}

public class CreateTicketTemplateRequestValidator : AbstractValidator<CreateTicketTemplateRequest>
{
    public CreateTicketTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(200).WithMessage("Template name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.TitleTemplate)
            .MaximumLength(500).WithMessage("Title template must not exceed 500 characters.");

        RuleFor(x => x.DescriptionTemplate)
            .MaximumLength(10000).WithMessage("Description template must not exceed 10000 characters.");

        RuleFor(x => x.DefaultType)
            .IsInEnum().WithMessage("Invalid default type.");

        RuleFor(x => x.DefaultPriority)
            .IsInEnum().WithMessage("Invalid default priority.");

        RuleFor(x => x.DefaultImpact)
            .IsInEnum().WithMessage("Invalid default impact.");

        RuleFor(x => x.DefaultUrgency)
            .IsInEnum().WithMessage("Invalid default urgency.");
    }
}

public class UpdateTicketTemplateRequestValidator : AbstractValidator<UpdateTicketTemplateRequest>
{
    public UpdateTicketTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(200).WithMessage("Template name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.TitleTemplate)
            .MaximumLength(500).WithMessage("Title template must not exceed 500 characters.");

        RuleFor(x => x.DescriptionTemplate)
            .MaximumLength(10000).WithMessage("Description template must not exceed 10000 characters.");

        RuleFor(x => x.DefaultType)
            .IsInEnum().WithMessage("Invalid default type.");

        RuleFor(x => x.DefaultPriority)
            .IsInEnum().WithMessage("Invalid default priority.");

        RuleFor(x => x.DefaultImpact)
            .IsInEnum().WithMessage("Invalid default impact.");

        RuleFor(x => x.DefaultUrgency)
            .IsInEnum().WithMessage("Invalid default urgency.");
    }
}

public class SubmitValidationRequestValidator : AbstractValidator<SubmitValidationRequest>
{
    public SubmitValidationRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid validation status.");

        RuleFor(x => x.Comment)
            .MaximumLength(2000).WithMessage("Comment must not exceed 2000 characters.");
    }
}

public class CreateTicketCostRequestValidator : AbstractValidator<CreateTicketCostRequest>
{
    public CreateTicketCostRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Cost description is required.")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(3).WithMessage("Currency must not exceed 3 characters.");

        RuleFor(x => x.CostType)
            .IsInEnum().WithMessage("Invalid cost type.");

        RuleFor(x => x.Reference)
            .MaximumLength(200).WithMessage("Reference must not exceed 200 characters.");
    }
}
