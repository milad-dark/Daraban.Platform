using Daraban.Modules.Inventory.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Inventory.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 1.1 SS1). Plain DI registration, no MediatR handler scanning.</summary>
public static class InventoryModuleServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(InventoryModuleServiceCollectionExtensions).Assembly);

        // TODO: register this module's I<Resource>Service / I<Resource>Repository pairs here
        // as they're built out (see Identity/Assets for the concrete pattern).

        return services;
    }
}
