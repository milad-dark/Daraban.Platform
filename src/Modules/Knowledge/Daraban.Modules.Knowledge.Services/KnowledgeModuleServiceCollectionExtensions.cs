using Daraban.Modules.Knowledge.Data;
using Daraban.Modules.Knowledge.Data.Repositories;
using Daraban.Modules.Knowledge.Services.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Knowledge.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 1.1 SS1). Plain DI registration, no MediatR handler scanning.</summary>
public static class KnowledgeModuleServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                // Without this, every module's migrations would share one public.__EFMigrationsHistory
                // table and step on each other. Keeping the history inside the module's own schema is
                // what makes "each module owns its own migrations" actually true.
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "knowledge")));

        services.AddValidatorsFromAssembly(typeof(KnowledgeModuleServiceCollectionExtensions).Assembly);

        // Task 8.2: shared memory cache for KbArticleService read-model caching. Deliberately no
        // SizeLimit here -- MemoryCacheOptions is host-wide and Dashboard already sets one; every
        // cached entry reports Size=1 so a limit (when present) stays consistent.
        services.AddMemoryCache();
        services.Configure<KbCacheOptions>(configuration.GetSection(KbCacheOptions.SectionName));

        // ---- Repositories (Task 6.4) ----
        services.AddScoped<IKbCategoryRepository, KbCategoryRepository>();
        services.AddScoped<IKbArticleRepository, KbArticleRepository>();
        services.AddScoped<IKbFeedbackRepository, KbFeedbackRepository>();
        services.AddScoped<IKbTicketLinkRepository, KbTicketLinkRepository>();

        // ---- Services (Task 6.4) ----
        services.AddScoped<IKbCategoryService, KbCategoryService>();
        services.AddScoped<IKbArticleService, KbArticleService>();
        services.AddScoped<IKbTicketLinkService, KbTicketLinkService>();

        return services;
    }
}
