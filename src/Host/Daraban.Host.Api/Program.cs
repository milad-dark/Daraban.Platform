using Daraban.Modules.Assets.Services;
using Daraban.Modules.Automation.Services;
using Daraban.Modules.Financial.Services;
using Daraban.Modules.Identity.Services;
using Daraban.Modules.Inventory.Services;
using Daraban.Modules.Knowledge.Services;
using Daraban.Modules.Notifications.Services;
using Daraban.Modules.Reporting.Services;
using Daraban.Modules.ServiceDesk.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration (Task 2.1 SS4 / see appsettings.json) -----------------
builder.Configuration.AddEnvironmentVariables(prefix: "DARABAN_");

// ---- Logging ---------------------------------------------------------------
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// ---- Module registration (Task 1.1 SS2.3: composition root wires every module) ----
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddAssetsModule(builder.Configuration)
    .AddInventoryModule(builder.Configuration)
    .AddServiceDeskModule(builder.Configuration)
    .AddFinancialModule(builder.Configuration)
    .AddKnowledgeModule(builder.Configuration)
    .AddAutomationModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration)
    .AddReportingModule(builder.Configuration);

// ---- Controllers: every module's *.Api assembly must be added as an MVC
// Application Part -- ASP.NET Core does NOT auto-discover controllers living in a
// referenced-but-not-entry assembly (Task 1.4 note carried into Daraban.Modules.*.Api's
// AssemblyMarker.cs). -----------------------------------------------------------------
var mvcBuilder = builder.Services.AddControllers();
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Identity.Api.AssemblyMarker).Assembly);
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Assets.Api.AssemblyMarker).Assembly);
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Inventory.Api.AssemblyMarker).Assembly);
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.ServiceDesk.Api.AssemblyMarker).Assembly);
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Financial.Api.AssemblyMarker).Assembly);
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Knowledge.Api.AssemblyMarker).Assembly);
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Automation.Api.AssemblyMarker).Assembly);
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Notifications.Api.AssemblyMarker).Assembly);
mvcBuilder.AddApplicationPart(typeof(Daraban.Modules.Reporting.Api.AssemblyMarker).Assembly);

// ---- Auth (Task 1.3): validates JWTs issued by the OpenIddict server ----------
// TODO: builder.Services.AddOpenIddict().AddValidation(...) -- full client registration
// (Authority, Audience, token validation params) lands once the auth server project
// itself is scaffolded (Task 2.2+). Placeholder scheme registration only for now so the
// rest of the pipeline (UseAuthentication/UseAuthorization) is wired correctly.
builder.Services.AddAuthentication(OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

// ---- CORS: only the Angular dev/prod origins from configuration --------------
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
          .AllowCredentials()
          .AllowAnyHeader()
          .AllowAnyMethod()));

// ---- Redis (permission cache, Task 1.3 SS4.3; dashboard widget cache, Task 1.1 SS6) --
//builder.Services.AddStackExchangeRedisCache(o =>
//    o.Configuration = builder.Configuration.GetConnectionString("Redis"));

// ---- SignalR (DashboardHub / TicketHub, Task 1.1 SS4/SS2.3) -------------------
builder.Services.AddSignalR();

// ---- Swagger / OpenAPI (also the source for Angular's generated HTTP client) --
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// app.MapHub<DashboardHub>("/hubs/dashboard");
// app.MapHub<TicketHub>("/hubs/tickets");

app.Run();
