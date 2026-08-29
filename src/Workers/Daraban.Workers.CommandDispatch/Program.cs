using Daraban.Modules.Identity.Services;
using Daraban.Platform.Messaging;
using Daraban.Workers.CommandDispatch;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
builder.Services.AddSerilog();

// Identity module (provides IAgentCommandService + IAgentCommandRepository + IdentityDbContext)
builder.Services.AddIdentityModule(builder.Configuration);

// SignalR hub context for pushing commands to connected agents
builder.Services.AddSignalR();

// RabbitMQ.Client consumer + publisher infrastructure
// Publisher is needed for CommandTimeoutService to emit AgentCommandTimedOutEvent
builder.Services.AddRabbitMqMessaging(builder.Configuration);

// Command dispatch consumer + timeout checker
builder.Services.AddHostedService<AgentCommandConsumer>();
builder.Services.AddHostedService<CommandTimeoutService>();

var host = builder.Build();
host.Run();
