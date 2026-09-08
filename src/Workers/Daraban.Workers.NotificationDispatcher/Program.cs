using Daraban.Platform.Hosting;
using Daraban.Platform.Messaging;
using Daraban.Workers.NotificationDispatcher;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

// Shared Serilog setup (console + rolling file) owned by Daraban.Platform.Hosting.
builder.UseDarabanSerilog(applicationName: "Daraban.Workers.NotificationDispatcher");

// Pure RabbitMQ.Client (Task: MassTransit removed -- see Daraban.Platform.Messaging for why).
builder.Services.AddRabbitMqConsumerInfrastructure(builder.Configuration);
builder.Services.AddHostedService<QueuedNotificationConsumer>();

var host = builder.Build();
host.Run();
