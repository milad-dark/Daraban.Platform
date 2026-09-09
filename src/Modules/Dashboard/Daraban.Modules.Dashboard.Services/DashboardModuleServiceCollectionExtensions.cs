using Daraban.Modules.Dashboard.Data;
using Daraban.Modules.Dashboard.Data.Repositories;
using Daraban.Modules.Dashboard.Services.Interfaces;
using Daraban.Modules.Dashboard.Services.Widgets;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Dashboard.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 1.1 SS1). Plain DI registration, no MediatR handler scanning.</summary>
public static class DashboardModuleServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DashboardDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(DashboardModuleServiceCollectionExtensions).Assembly);

        // Repositories
        services.AddScoped<IDashboardLayoutRepository, DashboardLayoutRepository>();

        // Services
        services.AddScoped<IDashboardService, DashboardService>();

        // Widget providers -- concrete registrations + one IWidgetDataProvider alias each.
        // The aliases feed DashboardService's IEnumerable<IWidgetDataProvider>; adding a new
        // widget later means two lines here, nothing else.
        services.AddScoped<OpenTicketsCountProvider>();
        services.AddScoped<TicketsByStatusProvider>();
        services.AddScoped<TicketsByPriorityProvider>();
        services.AddScoped<AssetCountByTypeProvider>();
        services.AddScoped<AssetsNearingEndProvider>();
        services.AddScoped<RecentInventoryProvider>();
        services.AddScoped<SlaComplianceRateProvider>();
        services.AddScoped<AgentStatusSummaryProvider>();

        services.AddScoped<IWidgetDataProvider>(sp => sp.GetRequiredService<OpenTicketsCountProvider>());
        services.AddScoped<IWidgetDataProvider>(sp => sp.GetRequiredService<TicketsByStatusProvider>());
        services.AddScoped<IWidgetDataProvider>(sp => sp.GetRequiredService<TicketsByPriorityProvider>());
        services.AddScoped<IWidgetDataProvider>(sp => sp.GetRequiredService<AssetCountByTypeProvider>());
        services.AddScoped<IWidgetDataProvider>(sp => sp.GetRequiredService<AssetsNearingEndProvider>());
        services.AddScoped<IWidgetDataProvider>(sp => sp.GetRequiredService<RecentInventoryProvider>());
        services.AddScoped<IWidgetDataProvider>(sp => sp.GetRequiredService<SlaComplianceRateProvider>());
        services.AddScoped<IWidgetDataProvider>(sp => sp.GetRequiredService<AgentStatusSummaryProvider>());

        // Widget tuning knobs (warranty horizon, SLA window) from "Dashboard" config section.
        services.Configure<WidgetOptions>(configuration.GetSection("Dashboard"));

        return services;
    }
}
