using Daraban.Modules.Software.Data;
using Daraban.Modules.Software.Data.Repositories;
using Daraban.Modules.Software.Services.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Software.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs. Plain DI registration, no MediatR handler scanning.</summary>
public static class SoftwareModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSoftwareModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SoftwareDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(SoftwareModuleServiceCollectionExtensions).Assembly);

        // Repositories
        services.AddScoped<ISoftwareRepository, SoftwareRepository>();
        services.AddScoped<ISoftwareLicenseRepository, SoftwareLicenseRepository>();
        services.AddScoped<ISoftwareInstallationRepository, SoftwareInstallationRepository>();

        // Services
        services.AddScoped<ISoftwareService, SoftwareService>();
        services.AddScoped<ISoftwareLicenseService, SoftwareLicenseService>();
        services.AddScoped<ISoftwareInstallationService, SoftwareInstallationService>();

        return services;
    }
}
