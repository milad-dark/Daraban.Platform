using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Daraban.Platform.Hosting;

/// <summary>
/// One logging setup shared by both hosts and all three workers instead of five copies of
/// the same LoggerConfiguration (Task 2.2). Structured console output always; a rolling
/// file sink too, since "docker logs" / "journalctl" retention is often shorter than you'd
/// like for actually debugging something that happened yesterday.
///
/// Two overloads because the hosts (WebApplicationBuilder) and the workers
/// (HostApplicationBuilder, from Host.CreateApplicationBuilder) expose logging
/// configuration differently -- WebApplicationBuilder.Host is an IHostBuilder,
/// HostApplicationBuilder is not. Same underlying LoggerConfiguration either way.
/// </summary>
public static class SerilogConfigurationExtensions
{
    public static IHostBuilder UseDarabanSerilog(this IHostBuilder hostBuilder, string applicationName)
        => hostBuilder.UseSerilog((context, loggerConfig) =>
            Configure(loggerConfig, context.Configuration, context.HostingEnvironment, applicationName));

    public static HostApplicationBuilder UseDarabanSerilog(this HostApplicationBuilder builder, string applicationName)
    {
        var loggerConfig = new LoggerConfiguration();
        Configure(loggerConfig, builder.Configuration, builder.Environment, applicationName);
        Log.Logger = loggerConfig.CreateLogger();
        builder.Services.AddSerilog();
        return builder;
    }

    private static void Configure(
        LoggerConfiguration loggerConfig, IConfiguration configuration, IHostEnvironment environment, string applicationName)
    {
        loggerConfig
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] ({Application}/{ThreadId}) {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: $"logs/{applicationName}-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14, // ~2 weeks -- long enough to debug "what happened last week", short enough not to fill a disk unattended
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Application}) {Message:lj} {Properties:j}{NewLine}{Exception}");
    }
}
