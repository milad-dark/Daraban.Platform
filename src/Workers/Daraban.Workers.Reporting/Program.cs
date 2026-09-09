using Daraban.Modules.Reporting.Services;
using Daraban.Platform.Hosting;
using Daraban.Platform.Messaging;
using Daraban.Workers.Reporting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

// Shared Serilog setup (console + rolling file) owned by Daraban.Platform.Hosting.
builder.UseDarabanSerilog(applicationName: "Daraban.Workers.Reporting");

// Publisher + connection provider: this worker only consumes, but keeping AddRabbitMqMessaging
// is harmless and leaves the door open for progress events without a second registration line.
builder.Services.AddRabbitMqMessaging(builder.Configuration);

// Reporting module services: DbContext, repositories, providers, renderers, file store.
builder.Services.AddReportingModule(builder.Configuration);

builder.Services.AddHostedService<ReportGenerationConsumer>();

var host = builder.Build();
host.Run();
