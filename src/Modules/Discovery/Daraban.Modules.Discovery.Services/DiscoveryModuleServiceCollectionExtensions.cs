using Daraban.Modules.Discovery.Data;
using Daraban.Modules.Discovery.Data.Repositories;
using Daraban.Modules.Discovery.Services.Snmp;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Discovery.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 5.1). Plain DI registration, no MediatR handler scanning.</summary>
public static class DiscoveryModuleServiceCollectionExtensions
{
    public static IServiceCollection AddDiscoveryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DiscoveryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(DiscoveryModuleServiceCollectionExtensions).Assembly);

        // Discovery module (Task 5.1 + 5.2)
        services.AddScoped<IDiscoveryRepository, DiscoveryRepository>();
        services.AddScoped<ICredentialEncryptionService, CredentialEncryptionService>();
        services.AddScoped<ISnmpDiscoveryEngine, SnmpDiscoveryEngine>();
        services.AddScoped<IDiscoveryService, DiscoveryService>();

        return services;
    }
}
