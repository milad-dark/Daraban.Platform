using Daraban.Host.AgentApi.Authorization;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services;
using Daraban.Modules.Identity.Services.Agents;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Modules.Inventory.Services;
using Daraban.Platform.Hosting;
using Daraban.Platform.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

// ---- Logging (Task 2.2): shared Serilog setup -- console + rolling file, structured. ----
builder.Host.UseDarabanSerilog(applicationName: "Daraban.Host.AgentApi");

// ---- Exception handling + ProblemDetails (Task 2.2) -----------------------------------
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions["instance"] = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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
// signs with the same JwtSigningKeyProvider key). We validate with JwtBearer (not OpenIddict)
// because both token issuers share the same signing key in-process. The is_agent claim +
// scope claims differentiate agent tokens from user tokens. -------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSigningKeyProvider>((options, keyProvider) =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(keyProvider.GetKey()),
        };
        // NOTE: We do NOT reject non-agent tokens at the host level here.
        // The AgentManagementController is for human admins managing agents — it
        // uses user JWTs, not agent JWTs. The is_agent check is enforced per-endpoint
        // by AgentScopeAuthorizationHandler (which only activates for agent:scope:*
        // policies). Plain [Authorize] on non-agent endpoints falls through to the
        // default policy provider and requires a valid user JWT.
    });

// ---- Authorization: AgentScope policy checks the scope claim in the agent's JWT ----
builder.Services.AddSingleton<IAuthorizationPolicyProvider, AgentScopePolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, AgentScopeAuthorizationHandler>();
builder.Services.AddAuthorization();

// ---- Agent services (Task 4.1: Communication Design) ----
builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IAgentAuthService, AgentAuthService>();

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
app.MapHub<Daraban.Host.AgentApi.Hubs.AgentControlHub>("/hubs/agent-control");

app.Run();
