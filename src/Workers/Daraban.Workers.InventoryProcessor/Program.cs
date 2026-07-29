using Daraban.Platform.Messaging;
using Daraban.Workers.InventoryProcessor;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
builder.Services.AddSerilog();

// Pure RabbitMQ.Client (Task: MassTransit removed -- see Daraban.Platform.Messaging for why).
builder.Services.AddRabbitMqConsumerInfrastructure(builder.Configuration);
builder.Services.AddHostedService<InventorySubmissionConsumer>();

var host = builder.Build();
host.Run();
