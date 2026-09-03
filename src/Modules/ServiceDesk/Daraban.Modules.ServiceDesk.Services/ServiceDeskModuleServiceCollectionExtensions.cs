using Daraban.Modules.ServiceDesk.Data;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.ServiceDesk.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 1.1 SS1). Plain DI registration, no MediatR handler scanning.</summary>
public static class ServiceDeskModuleServiceCollectionExtensions
{
    public static IServiceCollection AddServiceDeskModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ServiceDeskDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(ServiceDeskModuleServiceCollectionExtensions).Assembly);

        // Repositories
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketTaskRepository, TicketTaskRepository>();
        services.AddScoped<ITicketTemplateRepository, TicketTemplateRepository>();

        // Services
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ITicketTaskService, TicketTaskService>();
        services.AddScoped<ITicketTemplateService, TicketTemplateService>();

        return services;
    }
}
