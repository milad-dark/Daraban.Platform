using Daraban.Modules.Identity.Services;
using Daraban.Platform.Hosting;
using Daraban.Platform.Messaging;
using Daraban.Workers.CommandDispatch;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

// Shared Serilog setup (console + rolling file) owned by Daraban.Platform.Hosting.
builder.UseDarabanSerilog(applicationName: "Daraban.Workers.CommandDispatch");

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
