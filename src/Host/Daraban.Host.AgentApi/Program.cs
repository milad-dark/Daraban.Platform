using Daraban.Modules.Inventory.Services;
using Daraban.Platform.Messaging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Host.AgentApi only needs the modules an agent actually talks to -- Inventory for
// submissions/agents, plus a read-only slice of Assets for matching (Task 1.1 SS2.3).
builder.Services.AddInventoryModule(builder.Configuration);

var mvcBuilder = builder.Services.AddControllers();
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Inventory.Api.AssemblyMarker).Assembly);

// ---- Auth: agent tokens only, scope-checked rather than the user RBAC pipeline
// (Task 1.3 SS2.2) ------------------------------------------------------------------
builder.Services.AddAuthentication(OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
// TODO: AddOpenIddictValidation() client config + AddAuthorizationBuilder().AddPolicy("AgentScope", ...)
builder.Services.AddAuthorization();

// ---- RabbitMQ publisher for raw inventory submissions (Task 1.1 SS5.4) ------------
// Pure RabbitMQ.Client, not MassTransit -- MassTransit's newer versions require a
// commercial license for the features this project would actually use; RabbitMQ.Client
// is the official, always-free client maintained by the RabbitMQ team itself.
builder.Services.AddRabbitMqMessaging(builder.Configuration);

builder.Services.AddSignalR(); // AgentControlHub -- server -> agent push (Task 1.1 SS2.3)

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// app.MapHub<AgentControlHub>("/hubs/agent-control");

app.Run();
