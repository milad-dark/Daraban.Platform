using Daraban.Platform.Hosting;
using Daraban.Platform.Messaging;
using Daraban.Workers.InventoryProcessor;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

// Shared Serilog setup (console + rolling file) owned by Daraban.Platform.Hosting.
builder.UseDarabanSerilog(applicationName: "Daraban.Workers.InventoryProcessor");

// Pure RabbitMQ.Client (Task: MassTransit removed -- see Daraban.Platform.Messaging for why).
builder.Services.AddRabbitMqConsumerInfrastructure(builder.Configuration);
builder.Services.AddHostedService<InventorySubmissionConsumer>();

var host = builder.Build();
host.Run();
