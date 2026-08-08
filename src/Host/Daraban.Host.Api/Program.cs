using Daraban.Modules.Assets.Services;
using Daraban.Modules.Automation.Services;
using Daraban.Modules.Financial.Services;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Modules.Inventory.Services;
using Daraban.Modules.Knowledge.Services;
using Daraban.Modules.Notifications.Services;
using Daraban.Modules.Reporting.Services;
using Daraban.Modules.ServiceDesk.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
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

// ---- Auth (Task 2.3): validates JWTs issued directly by AuthService/JwtTokenService ---
// (a plain JWT Bearer setup, not OpenIddict -- see AuthController's doc comment for why
// the original OpenIddict Authorization Code + PKCE plan from Task 1.3 was descoped here).
// JwtSigningKeyProvider is registered as a singleton inside AddIdentityModule above, so the
// RSA key resolved here for *validation* is guaranteed to be the exact same instance
// JwtTokenService uses to *sign* -- both run in this same process.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Daraban.Modules.Identity.Services.Auth.JwtSigningKeyProvider>((options, keyProvider) =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false; // keep claim names exactly as issued (e.g. "token_version"), not remapped to long XML-namespace URIs
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30), // tight -- these are already short-lived (15 min) tokens, not the 5-minute default
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(keyProvider.GetKey()),
        };
        options.Events = new JwtBearerEvents
        {
            // token_version revocation (Task 1.3 SS8): rejects an otherwise-still-valid JWT
            // the instant identity.users.token_version no longer matches what was embedded
            // at issuance -- password change, forced logout, or an admin disabling the
            // account all take effect immediately instead of waiting out the token's exp.
            OnTokenValidated = async context =>
            {
                var tokenVersionClaim = context.Principal?.FindFirst("token_version")?.Value;
                var subClaim = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (tokenVersionClaim is null || subClaim is null || !Guid.TryParse(subClaim, out var userId))
                {
                    context.Fail("Malformed token.");
                    return;
                }

                var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var user = await users.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive || user.TokenVersion.ToString() != tokenVersionClaim)
                    context.Fail("Token has been revoked.");
            },
        };
    });
builder.Services.AddAuthorization();

// ---- Rate limiting (Task 2.3): auth endpoints are the classic brute-force/credential-
// stuffing target -- this is a per-IP throttle sitting in front of AuthService's own
// per-account lockout, not a replacement for it. ---------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0; // reject immediately over the limit -- these are interactive attempts, not background work worth queuing
    });
});

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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// app.MapHub<DashboardHub>("/hubs/dashboard");
// app.MapHub<TicketHub>("/hubs/tickets");

app.Run();
