using Daraban.Modules.Reporting.Services.Dtos;
using Daraban.Modules.Reporting.Services.Reports;
using Daraban.Modules.Reporting.Services.Rendering;
using FluentValidation;

namespace Daraban.Modules.Reporting.Services.Validators;

/// <summary>
/// Definition validation (Task 7.2). Name/format/module/columns are checked against the
/// closed ReportCatalog and ReportFormats; the optional schedule must be a parseable 5-field
/// cron expression (Cronos, shared with the Automation scheduler's parser).
/// </summary>
public sealed class CreateReportDefinitionRequestValidator : AbstractValidator<CreateReportDefinitionRequest>
{
    public CreateReportDefinitionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Module)
            .NotEmpty().WithMessage("Module is required.")
            .Must(m => ReportCatalog.TryGet(m, out _))
            .WithMessage(x => $"'{x.Module}' is not a reportable dataset.");

        RuleFor(x => x.Format)
            .Must(ReportFormats.IsSupported)
            .WithMessage(x => $"Format '{x.Format}' is not supported. Supported: {string.Join(", ", ReportFormats.All)}.");

        // Columns are optional (default = all); provided keys must exist in the dataset.
        RuleFor(x => x.Columns)
            .Must((request, columns) => ValidateColumns(request.Module, columns))
            .WithMessage("One or more columns are not part of the selected dataset.");

        // Schedule: required when enabled, must be a real cron expression, at least daily
        // (guard against somebody scheduling a report every second and hammering the worker).
        RuleFor(x => x.Schedule)
            .Must(BeValidCron)
            .When(x => !string.IsNullOrWhiteSpace(x.Schedule))
            .WithMessage("Schedule must be a valid cron expression (minute hour day month weekday).");

        RuleFor(x => x.Schedule)
            .NotEmpty()
            .When(x => x.ScheduleEnabled)
            .WithMessage("A schedule is required when the definition is schedule-enabled.");
    }

    private static bool ValidateColumns(string? module, IReadOnlyList<string>? columns)
    {
        if (columns is null || columns.Count == 0) return true;
        if (!ReportCatalog.TryGet(module, out var metadata) || metadata is null) return false;
        return ReportCatalog.ColumnsAreValid(metadata, columns);
    }

    private static bool BeValidCron(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return false;
        return CronExpressionValidator.TryValidate(cron, out _);
    }
}
