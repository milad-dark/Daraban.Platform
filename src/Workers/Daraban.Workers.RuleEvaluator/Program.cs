using Daraban.Modules.Reporting.Services;
using Daraban.Platform.Hosting;
using Daraban.Platform.Messaging;
using Daraban.Workers.RuleEvaluator;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

// Shared Serilog setup (console + rolling file) owned by Daraban.Platform.Hosting.
builder.UseDarabanSerilog(applicationName: "Daraban.Workers.RuleEvaluator");

// Pure RabbitMQ.Client (Task: MassTransit removed -- see Daraban.Platform.Messaging for why).
// Publisher registration is required: the scheduled-report cron publishes ReportRequestedEvent.
builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddHostedService<RuleEvaluationConsumer>();

// Scheduled reports (Task 7.2): Reporting's repositories + cron runner. The reporting DbContext
// registration needs the connection string; the Reporting module's own extension handles it.
builder.Services.AddReportingModule(builder.Configuration);
builder.Services.AddHostedService<ReportScheduleCronService>();

var host = builder.Build();
host.Run();
