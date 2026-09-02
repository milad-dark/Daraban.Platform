using Daraban.Modules.Discovery.Services;
using Daraban.Workers.Discovery;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
builder.Services.AddSerilog();

// Discovery module (provides IDiscoveryService + repository + DbContext)
builder.Services.AddDiscoveryModule(builder.Configuration);

// IP Scanner engine
builder.Services.AddSingleton<IPScannerOptions>(new IPScannerOptions
{
    MaxConcurrentPings = int.TryParse(builder.Configuration["Discovery:MaxConcurrentPings"], out var pings) ? pings : 50,
    MaxConcurrentPortScans = int.TryParse(builder.Configuration["Discovery:MaxConcurrentPortScans"], out var scans) ? scans : 100,
    PingTimeoutMs = int.TryParse(builder.Configuration["Discovery:PingTimeoutMs"], out var pingTimeout) ? pingTimeout : 2000,
    PortScanTimeoutMs = int.TryParse(builder.Configuration["Discovery:PortScanTimeoutMs"], out var portTimeout) ? portTimeout : 1000,
    DeviceDelayMs = int.TryParse(builder.Configuration["Discovery:DeviceDelayMs"], out var delay) ? delay : 100
});
builder.Services.AddSingleton<IIPScanner, IPScannerEngine>();

// Background scan worker
builder.Services.AddHostedService<ScanWorker>();

var host = builder.Build();
host.Run();
