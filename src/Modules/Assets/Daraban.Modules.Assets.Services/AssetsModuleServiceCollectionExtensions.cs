using Daraban.Modules.Assets.Data;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Assets.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs. Plain DI registration, no MediatR handler scanning.</summary>
public static class AssetsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAssetsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AssetsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(AssetsModuleServiceCollectionExtensions).Assembly);

        // ---- Repositories ----
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IAssetTypeRepository, AssetTypeRepository>();
        services.AddScoped<IAssetCategoryRepository, AssetCategoryRepository>();
        services.AddScoped<IAssetAssignmentRepository, AssetAssignmentRepository>();
        services.AddScoped<IAssetStatusHistoryRepository, AssetStatusHistoryRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IManufacturerRepository, ManufacturerRepository>();

        // ---- Services (Task 3.2: CRUD) ----
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAssetTypeService, AssetTypeService>();
        services.AddScoped<IAssetCategoryService, AssetCategoryService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IManufacturerService, ManufacturerService>();

        // ---- Services (Task 3.3: Assignment) ----
        services.AddScoped<IAssetAssignmentService, AssetAssignmentService>();

        // ---- Services (Task 3.4: Lifecycle) ----
        services.AddScoped<IAssetLifecycleService, AssetLifecycleService>();

        return services;
    }
}
