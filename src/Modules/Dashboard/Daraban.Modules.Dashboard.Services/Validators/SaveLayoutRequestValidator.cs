using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.Dashboard.Services.Widgets;
using FluentValidation;

namespace Daraban.Modules.Dashboard.Services.Validators;

/// <summary>
/// Server-side guard for the layout designer payload (Task 7.1). The frontend also validates,
/// but the server is the boundary: this stops oversized grids, unknown widget types, and
/// duplicate placements from being persisted (OWASP A04: never trust client input).
/// </summary>
public sealed class SaveLayoutRequestValidator : AbstractValidator<SaveLayoutRequest>
{
    public const int MaxWidgets = 24;
    public const int MaxGridX = 11; // 12-column grid, 0-based
    public const int MaxGridY = 49; // 50-row practical cap

    public SaveLayoutRequestValidator()
    {
        RuleFor(r => r.Widgets)
            .NotNull()
            .NotEmpty().WithMessage("A layout must contain at least one widget.")
            .Must(w => w.Count <= MaxWidgets)
            .WithMessage($"A layout may contain at most {MaxWidgets} widgets.");

        RuleForEach(r => r.Widgets).ChildRules(w =>
        {
            w.RuleFor(p => p.WidgetType)
                .Must(t => WidgetCatalog.TryParse(t, out _))
                .WithMessage("Unknown widget type '{PropertyValue}'.");

            w.RuleFor(p => p.X).InclusiveBetween(0, MaxGridX);
            w.RuleFor(p => p.Y).InclusiveBetween(0, MaxGridY);
            w.RuleFor(p => p.W).InclusiveBetween(1, 12);
            w.RuleFor(p => p.H).InclusiveBetween(1, 8);
        });

        // Duplicate widget instances (same type twice) would double-render data -- reject them.
        RuleFor(r => r.Widgets)
            .Must(w => w.Select(x => x.WidgetType).Distinct().Count() == w.Count)
            .WithMessage("Each widget type may appear at most once.");
    }
}
