using Daraban.Host.AgentApi.Authorization;
using Daraban.Modules.Identity.Services;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Modules.Identity.Services.Hubs;
using Daraban.Modules.Inventory.Services;
using Daraban.Platform.Hosting;
using Daraban.Platform.Messaging;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

// ---- Logging (Task 2.2): shared Serilog setup -- console + rolling file, structured. ----
builder.Host.UseDarabanSerilog(applicationName: "Daraban.Host.AgentApi");

// ---- Exception handling + ProblemDetails (Task 2.2) -----------------------------------
builder.Services.AddDarabanProblemDetails();

// ---- Health checks (Task 2.2): this host's readiness depends on Postgres + RabbitMQ,
// not Redis (it doesn't use it). ---------------------------------------------------------
builder.Services.AddDarabanHealthChecks(builder.Configuration);

// Host.AgentApi needs Identity (for JwtSigningKeyProvider + Agent services) and
// Inventory for submissions/agents (Task 1.1 SS2.3).
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);

var mvcBuilder = builder.Services.AddControllers();
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Inventory.Api.AssemblyMarker).Assembly);

// ---- Auth: agent tokens use the same RSA-signed JWTs as user tokens (AgentAuthService
// signs with the same JwtSigningKeyProvider key). Validation policy is owned by the
// Identity module (AddDarabanJwtBearer). NOTE: we do NOT reject non-agent tokens at the
// host level here -- the AgentManagementController is for human admins and uses user JWTs.
// The is_agent check is enforced per-endpoint by AgentScopeAuthorizationHandler (which only
// activates for agent:scope:* policies); plain [Authorize] requires a valid user JWT. ----
builder.Services.AddDarabanJwtBearer(builder.Configuration, requireHttpsMetadata: !builder.Environment.IsDevelopment());

// ---- Authorization: AgentScope policy checks the scope claim in the agent's JWT ----
builder.Services.AddSingleton<IAuthorizationPolicyProvider, AgentScopePolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, AgentScopeAuthorizationHandler>();
builder.Services.AddAuthorization();

// ---- RabbitMQ publisher for raw inventory submissions (Task 1.1 SS5.4) ------------
// Pure RabbitMQ.Client, not MassTransit -- MassTransit's newer versions require a
// commercial license for the features this project would actually use; RabbitMQ.Client
// is the official, always-free client maintained by the RabbitMQ team itself.
builder.Services.AddRabbitMqMessaging(builder.Configuration);

builder.Services.AddSignalR(); // AgentControlHub -- server -> agent push (Task 1.1 SS2.3)

// ---- CORS: permissive for agent-to-agent API calls (agents are machines, not browsers) --
builder.Services.AddCors(options => options.AddPolicy("Agents", policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();
app.UseCors("Agents");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDarabanHealthCheckEndpoints();
app.MapHub<AgentControlHub>("/hubs/agent-control");

app.Run();
