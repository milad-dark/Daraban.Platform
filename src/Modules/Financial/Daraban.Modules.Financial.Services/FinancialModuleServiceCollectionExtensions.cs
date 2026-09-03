using Daraban.Modules.Financial.Data;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Financial.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 1.1 SS1). Plain DI registration, no MediatR handler scanning.</summary>
public static class FinancialModuleServiceCollectionExtensions
{
    public static IServiceCollection AddFinancialModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FinancialDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(FinancialModuleServiceCollectionExtensions).Assembly);

        // Repositories
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IInfocomRepository, InfocomRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();

        // Services
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IInfocomService, InfocomService>();
        services.AddScoped<IPurchaseService, PurchaseService>();

        return services;
    }
}
