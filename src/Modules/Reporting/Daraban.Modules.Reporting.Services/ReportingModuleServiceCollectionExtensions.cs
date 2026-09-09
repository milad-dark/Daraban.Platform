using Daraban.Modules.Reporting.Data;
using Daraban.Modules.Reporting.Data.Repositories;
using Daraban.Modules.Reporting.Services.Interfaces;
using Daraban.Modules.Reporting.Services.Reports;
using Daraban.Modules.Reporting.Services.Rendering;
using Daraban.Modules.Reporting.Services.Storage;
using Daraban.Modules.Reporting.Services.Validators;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Reporting.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 1.1 SS1). Plain DI registration, no MediatR handler scanning.</summary>
public static class ReportingModuleServiceCollectionExtensions
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ReportingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(ReportingModuleServiceCollectionExtensions).Assembly);

        // Repositories
        services.AddScoped<IReportDefinitionRepository, ReportDefinitionRepository>();
        services.AddScoped<ISavedReportRepository, SavedReportRepository>();

        // Services
        services.AddScoped<IReportingService, ReportingService>();
        services.AddSingleton(TimeProvider.System);

        // File store (Task 7.2): local filesystem by default; swap the implementation here
        // when S3/Azure Blob land. The worker resolves the same interface for writing.
        services.Configure<ReportStoreOptions>(configuration.GetSection(ReportStoreOptions.SectionName));
        services.AddSingleton<IReportFileStore, LocalReportFileStore>();

        // Renderers -- concrete registrations + one IReportRenderer alias per format, the
        // same pattern the Dashboard module uses for widget providers.
        services.AddSingleton<CsvReportRenderer>();
        services.AddSingleton<ExcelReportRenderer>();
        services.AddSingleton<PdfReportRenderer>();
        services.AddSingleton<IReportRenderer>(sp => sp.GetRequiredService<CsvReportRenderer>());
        services.AddSingleton<IReportRenderer>(sp => sp.GetRequiredService<ExcelReportRenderer>());
        services.AddSingleton<IReportRenderer>(sp => sp.GetRequiredService<PdfReportRenderer>());

        // Data providers -- one per catalog dataset (registered by the worker too).
        services.AddScoped<TicketsReportDataProvider>();
        services.AddScoped<AssetsReportDataProvider>();
        services.AddScoped<AgentsReportDataProvider>();
        services.AddScoped<IReportDataProvider>(sp => sp.GetRequiredService<TicketsReportDataProvider>());
        services.AddScoped<IReportDataProvider>(sp => sp.GetRequiredService<AssetsReportDataProvider>());
        services.AddScoped<IReportDataProvider>(sp => sp.GetRequiredService<AgentsReportDataProvider>());

        return services;
    }
}
