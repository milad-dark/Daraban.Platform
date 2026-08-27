# Graph Report - Daraban.Platform  (2026-08-27)

## Corpus Check
- Corpus is ~29,142 words - fits in a single context window. You may not need a graph.

## Summary
- 2060 nodes · 3979 edges · 118 communities (101 shown, 17 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 279 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Assets Module
- Asset Assignments API
- Asset Categories API
- Auth & Authorization
- Angular Frontend Shell
- Locations API
- Authorization Handlers
- Infrastructure Config
- Auth Controller & Identity
- Asset Fields Entity
- Health Checks
- Asset Models Entity
- EF Core Configuration
- Claims & HTTP Context
- Asset Entity
- Import/Export Excel
- Computer Entity
- Community 17
- Community 18
- Community 19
- Community 20
- Community 21
- Community 22
- Community 23
- Community 24
- Community 25
- Community 26
- Community 27
- Community 28
- Community 29
- Community 30
- Community 31
- Community 33
- Community 34
- Community 35
- Community 36
- Community 37
- Community 38
- Community 39
- Community 40
- Community 41
- Community 42
- Community 43
- Community 44
- Community 45
- Community 46
- Community 47
- Community 48
- Community 49
- Community 50
- Community 51
- Community 52
- Community 53
- Community 54
- Community 55
- Community 56
- Community 57
- Community 58
- Community 59
- Community 60
- Community 61
- Community 62
- Community 63
- Community 64
- Community 65
- Community 66
- Community 67
- Community 68
- Community 69
- Community 70
- Community 71
- Community 72
- Community 73
- Community 74
- Community 75
- Community 76
- Community 77
- Community 78
- Community 79
- Community 80
- Community 81
- Community 82
- Community 83
- Community 84
- Community 85
- Community 86
- Community 87
- Community 88
- Community 89
- Community 90
- Community 91
- Community 92
- Community 93
- Community 94
- Community 95
- Community 96
- Community 97
- Community 98
- Community 99
- Community 100
- Community 101
- Community 102
- Community 103
- Community 104
- Community 105
- Community 106
- Community 107
- Community 108
- Community 109
- Community 110
- Community 111
- Community 112
- Community 113
- Community 114
- Community 115
- Community 116
- Community 117

## God Nodes (most connected - your core abstractions)
1. `Result` - 89 edges
2. `Asset` - 58 edges
3. `Daraban.Modules.Assets.Data.Entities` - 54 edges
4. `Daraban.Platform.Common` - 39 edges
5. `User` - 34 edges
6. `AssetAssignment` - 31 edges
7. `AssetCategory` - 30 edges
8. `AssetsDbContext` - 29 edges
9. `Daraban.Modules.Assets.Services.Interfaces` - 29 edges
10. `Daraban.Modules.Assets.Services.Dtos` - 26 edges

## Surprising Connections (you probably didn't know these)
- `Daraban Platform Development Overrides` --references--> `Daraban Platform Production Stack`  [EXTRACTED]
  docker-compose.override.yml → docker-compose.yml
- `Shell Component (App Layout)` --references--> `Frontend Service`  [INFERRED]
  frontend/src/app/core/layout/shell/shell.component.html → docker-compose.yml
- `Login Component` --semantically_similar_to--> `Register Component`  [INFERRED] [semantically similar]
  frontend/src/app/features/auth/login/login.component.html → frontend/src/app/features/auth/register/register.component.html
- `AssetAssignmentLookupController` --references--> `IAssetAssignmentService`  [EXTRACTED]
  src/Modules/Assets/Daraban.Modules.Assets.Api/Controllers/AssetAssignmentLookupController.cs → src/Modules/Assets/Daraban.Modules.Assets.Services/Interfaces/IAssetAssignmentService.cs
- `AssetLifecycleController` --references--> `ICurrentUser`  [EXTRACTED]
  src/Modules/Assets/Daraban.Modules.Assets.Api/Controllers/AssetLifecycleController.cs → src/Shared/Daraban.Platform.Abstractions/ICurrentUser.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Authentication UI Flow** — frontend_src_app_features_auth_login_login_component_html_logincomponent, frontend_src_app_features_auth_register_register_component_html_registercomponent, frontend_src_app_features_auth_login_login_component_html_authstore [INFERRED 0.90]
- **Message-Driven Worker Pattern** — docker_compose_workerautomation, docker_compose_workernotifications, docker_compose_workerreporting, docker_compose_rabbitmq [INFERRED 0.85]
- **Data Infrastructure Layer** — docker_compose_postgres, docker_compose_redis, docker_compose_rabbitmq, docker_compose_darabannet [EXTRACTED 1.00]

## Communities (118 total, 17 thin omitted)

### Community 0 - "Assets Module"
Cohesion: 0.06
Nodes (60): AssetType, AssetsDbContext, CancellationToken, Guid, IReadOnlyList, Task, AssetTypeRepository, CancellationToken (+52 more)

### Community 1 - "Asset Assignments API"
Cohesion: 0.06
Nodes (53): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, IActionResult, ObjectResult, RequirePermission (+45 more)

### Community 2 - "Asset Categories API"
Cohesion: 0.06
Nodes (50): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IActionResult, ObjectResult (+42 more)

### Community 3 - "Auth & Authorization"
Cohesion: 0.07
Nodes (21): AuthorizeAttribute, Daraban.Modules.Assets.Data.Repositories, Daraban.Modules.Assets.Services.Interfaces, Daraban.Platform.Common, Daraban.Modules.Assets.Services.Dtos, Daraban.Modules.Assets.Data.Entities, Daraban.Platform.Abstractions, Daraban.Modules.Assets.Api.Controllers (+13 more)

### Community 4 - "Angular Frontend Shell"
Cohesion: 0.05
Nodes (31): AppComponent, Component, appConfig, routes, authGuard(), authInterceptor(), AuthService, AuthState (+23 more)

### Community 5 - "Locations API"
Cohesion: 0.09
Nodes (35): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IActionResult, ObjectResult (+27 more)

### Community 6 - "Authorization Handlers"
Cohesion: 0.06
Nodes (33): AuthorizationHandler, AuthorizationHandlerContext, AuthorizationPolicy, DefaultAuthorizationPolicyProvider, IAuthorizationPolicyProvider, IAuthorizationRequirement, IDistributedCache, CancellationToken (+25 more)

### Community 7 - "Infrastructure Config"
Cohesion: 0.05
Nodes (30): Daraban.Modules.Inventory.Services, Daraban.Modules.Inventory.Data, Daraban.Platform.Hosting, Exception, HostApplicationBuilder, IExceptionHandler, IHealthChecksBuilder, IHostBuilder (+22 more)

### Community 8 - "Auth Controller & Identity"
Cohesion: 0.09
Nodes (30): Daraban.Modules.Identity.Services.Users, Daraban.Modules.Identity.Api.Controllers, CancellationToken, Guid, HttpGet, HttpPost, IActionResult, RequirePermission (+22 more)

### Community 9 - "Asset Fields Entity"
Cohesion: 0.05
Nodes (40): DateTimeOffset, Guid, ICollection, AssetField, AssetType, AssetTypeId, CreatedAt, DefaultValue (+32 more)

### Community 10 - "Health Checks"
Cohesion: 0.06
Nodes (30): AspNetCore.HealthChecks.NpgSql, AspNetCore.HealthChecks.Rabbitmq, AspNetCore.HealthChecks.Redis, AspNetCore.HealthChecks.UI.Client, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.EntityFrameworkCore.Relational, Microsoft.Extensions.Options.DataAnnotations, Serilog (+22 more)

### Community 11 - "Asset Models Entity"
Cohesion: 0.09
Nodes (28): DateTimeOffset, Guid, ICollection, AssetModel, Assets, AssetType, AssetTypeId, CreatedAt (+20 more)

### Community 12 - "EF Core Configuration"
Cohesion: 0.07
Nodes (21): Daraban.Modules.Assets.Data.Configurations, IEntityTypeConfiguration, ModelBuilder, EntityTypeBuilder, AssetAssignmentConfiguration, EntityTypeBuilder, AssetCategoryConfiguration, EntityTypeBuilder (+13 more)

### Community 13 - "Claims & HTTP Context"
Cohesion: 0.12
Nodes (24): ClaimsPrincipal, IHttpContextAccessor, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut (+16 more)

### Community 14 - "Asset Entity"
Cohesion: 0.06
Nodes (30): DateOnly, DateTimeOffset, Guid, ICollection, Asset, AssetModelId, AssetTag, AssetTypeId (+22 more)

### Community 15 - "Import/Export Excel"
Cohesion: 0.14
Nodes (17): Dictionary, ImportAssetRowMap, IXLRow, List, CancellationToken, DateOnly, Guid, Stream (+9 more)

### Community 16 - "Computer Entity"
Cohesion: 0.08
Nodes (27): DateTimeOffset, Guid, Computer, InventoryNumber, LastInventoryAt, LocationId, ManufacturerId, ModelId (+19 more)

### Community 17 - "Community 17"
Cohesion: 0.15
Nodes (15): CancellationToken, Guid, IReadOnlyList, Items, Task, TotalCount, IUserRepository, CancellationToken (+7 more)

### Community 18 - "Community 18"
Cohesion: 0.07
Nodes (26): angularCompilerOptions, enableI18nLegacyMessageIdFormat, strictInjectionParameters, strictInputAccessModifiers, strictTemplates, compileOnSave, compilerOptions, declaration (+18 more)

### Community 19 - "Community 19"
Cohesion: 0.09
Nodes (20): IDisposable, PasswordHasher, RSA, IJwtTokenService, IHostEnvironment, ILogger, JwtSigningKeyProvider, JwtTokenService (+12 more)

### Community 20 - "Community 20"
Cohesion: 0.18
Nodes (15): DateTimeOffset, Guid, AuthResult, AuthUserResponse, LoginRequest, RegisterRequest, CancellationToken, IPasswordHasher (+7 more)

### Community 21 - "Community 21"
Cohesion: 0.09
Nodes (20): Microsoft.Extensions.Identity.Core, Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, System.Security.Cryptography.Xml, Microsoft.NET.Sdk, FluentValidation, FluentValidation.DependencyInjectionExtensions (+12 more)

### Community 22 - "Community 22"
Cohesion: 0.21
Nodes (22): Daraban Bridge Network, Daraban Platform Production Stack, Frontend Service, Host Agent API Service, Host API Service, ASP.NET Core Runtime, JWT Authentication, Nginx Reverse Proxy (+14 more)

### Community 23 - "Community 23"
Cohesion: 0.10
Nodes (19): EntityTypeBuilder, AssetRelationshipConfiguration, DateTimeOffset, Guid, AssetRelationship, CreatedAt, Id, Notes (+11 more)

### Community 24 - "Community 24"
Cohesion: 0.10
Nodes (17): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, System.Security.Cryptography.Xml, Microsoft.NET.Sdk, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Configuration.Abstractions (+9 more)

### Community 25 - "Community 25"
Cohesion: 0.10
Nodes (21): @angular/cli, @angular/compiler-cli, @angular-devkit/build-angular, devDependencies, @angular/cli, @angular/compiler-cli, @angular-devkit/build-angular, karma (+13 more)

### Community 26 - "Community 26"
Cohesion: 0.24
Nodes (10): CancellationToken, Guid, Task, IRefreshTokenRepository, CancellationToken, Guid, NewToken, Task (+2 more)

### Community 27 - "Community 27"
Cohesion: 0.11
Nodes (19): @angular/animations, @angular/cdk, @angular/core, @angular/forms, @angular/platform-browser, @angular/platform-browser-dynamic, dependencies, @angular/animations (+11 more)

### Community 28 - "Community 28"
Cohesion: 0.17
Nodes (7): Daraban.Modules.Identity.Data.Repositories, Daraban.Modules.Identity.Data.Entities, Daraban.Modules.Identity.Services, Daraban.Modules.Identity.Data, Daraban.Modules.Identity.Services.Authorization, Daraban.Modules.Identity.Services.Auth, IdentityModuleServiceCollectionExtensions

### Community 29 - "Community 29"
Cohesion: 0.11
Nodes (18): DateTimeOffset, Guid, ICollection, Location, Address, Assets, Children, City (+10 more)

### Community 30 - "Community 30"
Cohesion: 0.11
Nodes (14): Microsoft.Extensions.Options, RabbitMQ.Client, Microsoft.Extensions.Hosting.Abstractions, Microsoft.Extensions.Options.ConfigurationExtensions, Microsoft.NET.Sdk, Serilog.AspNetCore, Serilog.Sinks.Console, Microsoft.NET.Sdk.Worker (+6 more)

### Community 31 - "Community 31"
Cohesion: 0.11
Nodes (15): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, System.Security.Cryptography.Xml, Microsoft.NET.Sdk, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Configuration.Abstractions (+7 more)

### Community 33 - "Community 33"
Cohesion: 0.29
Nodes (11): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IActionResult, ObjectResult (+3 more)

### Community 34 - "Community 34"
Cohesion: 0.29
Nodes (11): CancellationToken, Guid, HttpDelete, HttpGet, HttpPost, HttpPut, IActionResult, ObjectResult (+3 more)

### Community 35 - "Community 35"
Cohesion: 0.12
Nodes (14): EntityTypeBuilder, AssetDocumentConfiguration, DateTimeOffset, Guid, AssetDocument, Asset, AssetId, FilePath (+6 more)

### Community 36 - "Community 36"
Cohesion: 0.12
Nodes (14): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.NET.Sdk, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions (+6 more)

### Community 37 - "Community 37"
Cohesion: 0.12
Nodes (15): EntityTypeBuilder, RefreshTokenConfiguration, DateTimeOffset, Guid, RefreshToken, FamilyId, Id, IsActive (+7 more)

### Community 38 - "Community 38"
Cohesion: 0.12
Nodes (14): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.NET.Sdk, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions (+6 more)

### Community 39 - "Community 39"
Cohesion: 0.12
Nodes (14): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.NET.Sdk, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions (+6 more)

### Community 40 - "Community 40"
Cohesion: 0.12
Nodes (14): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.NET.Sdk, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions (+6 more)

### Community 41 - "Community 41"
Cohesion: 0.19
Nodes (14): AbstractValidator, Daraban.Modules.Assets.Services.Validators, LifecycleTransitionRequest, AssignAssetRequestValidator, CreateAssetCategoryRequestValidator, CreateAssetRequestValidator, CreateAssetTypeRequestValidator, CreateLocationRequestValidator (+6 more)

### Community 42 - "Community 42"
Cohesion: 0.13
Nodes (10): Daraban.Modules.Identity.Data.Configurations, EntityTypeBuilder, ProfileConfiguration, EntityTypeBuilder, UserConfiguration, Guid, Profile, Id (+2 more)

### Community 43 - "Community 43"
Cohesion: 0.13
Nodes (13): EntityTypeBuilder, AssetFieldValueConfiguration, DateTimeOffset, Guid, AssetFieldValue, Asset, AssetField, AssetFieldId (+5 more)

### Community 44 - "Community 44"
Cohesion: 0.28
Nodes (8): AssetsDbContext, CancellationToken, Guid, IReadOnlyList, Items, Task, TotalCount, AssetRepository

### Community 45 - "Community 45"
Cohesion: 0.19
Nodes (11): AssetsDbContext, CancellationToken, Guid, IReadOnlyList, Task, AssetStatusHistoryRepository, CancellationToken, Guid (+3 more)

### Community 46 - "Community 46"
Cohesion: 0.21
Nodes (11): CancellationToken, Guid, IReadOnlyList, Task, AssetLifecycleService, DateTimeOffset, AssetStatusHistoryDto, CancellationToken (+3 more)

### Community 47 - "Community 47"
Cohesion: 0.13
Nodes (14): DateTimeOffset, Guid, ICollection, Manufacturer, CreatedAt, DeletedAt, Id, IsActive (+6 more)

### Community 48 - "Community 48"
Cohesion: 0.14
Nodes (12): EntityTypeBuilder, EntityNodeConfiguration, DateTimeOffset, Guid, EntityNode, CreatedAt, FullPath, Id (+4 more)

### Community 49 - "Community 49"
Cohesion: 0.20
Nodes (11): AssetExportMap, ClassMap, CancellationToken, ContentType, FileName, Guid, IReadOnlyList, Stream (+3 more)

### Community 50 - "Community 50"
Cohesion: 0.19
Nodes (11): Consumes, IFormFile, CancellationToken, HttpGet, HttpPost, IActionResult, ObjectResult, ProducesResponseType (+3 more)

### Community 51 - "Community 51"
Cohesion: 0.14
Nodes (12): ClosedXML, CsvHelper, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.NET.Sdk, Microsoft.NET.Test.Sdk (+4 more)

### Community 52 - "Community 52"
Cohesion: 0.14
Nodes (11): OpenIddict.Validation.AspNetCore, Microsoft.AspNetCore.SignalR, Microsoft.NET.Sdk.Web, Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.NET.Sdk, Microsoft.EntityFrameworkCore (+3 more)

### Community 53 - "Community 53"
Cohesion: 0.14
Nodes (14): DbSet, AssetsDbContext, AssetAssignments, AssetCategories, AssetDocuments, AssetFields, AssetFieldValues, AssetModels (+6 more)

### Community 54 - "Community 54"
Cohesion: 0.31
Nodes (7): CancellationToken, Guid, IReadOnlyList, Items, Task, TotalCount, IAssetRepository

### Community 55 - "Community 55"
Cohesion: 0.14
Nodes (13): DateTimeOffset, Guid, User, DefaultEntityId, DisplayName, Email, EmailConfirmed, FailedLoginCount (+5 more)

### Community 56 - "Community 56"
Cohesion: 0.14
Nodes (11): FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Test.Sdk, Moq (+3 more)

### Community 57 - "Community 57"
Cohesion: 0.17
Nodes (11): ControllerBase, CancellationToken, Guid, HttpGet, IActionResult, ObjectResult, ProducesResponseType, RequirePermission (+3 more)

### Community 58 - "Community 58"
Cohesion: 0.19
Nodes (4): Daraban.Workers.RuleEvaluator, Daraban.Workers.NotificationDispatcher, Daraban.Platform.Messaging, Daraban.Workers.InventoryProcessor

### Community 59 - "Community 59"
Cohesion: 0.21
Nodes (13): options, assets, browser, index, outputPath, polyfills, scripts, styles (+5 more)

### Community 60 - "Community 60"
Cohesion: 0.37
Nodes (7): IWebHostEnvironment, CancellationToken, HttpPost, IActionResult, ObjectResult, Task, AuthController

### Community 61 - "Community 61"
Cohesion: 0.24
Nodes (10): CancellationToken, Guid, HttpGet, HttpPost, IActionResult, ObjectResult, RequirePermission, Task (+2 more)

### Community 62 - "Community 62"
Cohesion: 0.15
Nodes (12): DateTimeOffset, Guid, AssetStatusHistory, ActorUserId, Asset, AssetId, FromStatus, Id (+4 more)

### Community 63 - "Community 63"
Cohesion: 0.17
Nodes (10): EntityTypeBuilder, UserProfileEntityConfiguration, Guid, UserProfileEntity, EntityId, Id, IsDefault, IsRecursive (+2 more)

### Community 64 - "Community 64"
Cohesion: 0.18
Nodes (8): Daraban.Modules.Automation.Services, Daraban.Modules.Automation.Data, DbContext, ModelBuilder, AutomationDbContext, IConfiguration, IServiceCollection, AutomationModuleServiceCollectionExtensions

### Community 65 - "Community 65"
Cohesion: 0.18
Nodes (9): EntityTypeBuilder, ProfileRightConfiguration, Guid, ProfileRight, Action, Id, IsRecursive, Module (+1 more)

### Community 66 - "Community 66"
Cohesion: 0.21
Nodes (10): IConfiguration, IServiceCollection, MessagingServiceCollectionExtensions, RabbitMqOptions, ExchangeName, Host, Password, Port (+2 more)

### Community 67 - "Community 67"
Cohesion: 0.20
Nodes (7): Daraban.Modules.Reporting.Data, Daraban.Modules.Reporting.Services, ModelBuilder, ReportingDbContext, IConfiguration, IServiceCollection, ReportingModuleServiceCollectionExtensions

### Community 68 - "Community 68"
Cohesion: 0.20
Nodes (7): Daraban.Modules.Notifications.Services, Daraban.Modules.Notifications.Data, ModelBuilder, NotificationsDbContext, IConfiguration, IServiceCollection, NotificationsModuleServiceCollectionExtensions

### Community 69 - "Community 69"
Cohesion: 0.20
Nodes (7): Daraban.Modules.Financial.Services, Daraban.Modules.Financial.Data, ModelBuilder, FinancialDbContext, IConfiguration, IServiceCollection, FinancialModuleServiceCollectionExtensions

### Community 70 - "Community 70"
Cohesion: 0.20
Nodes (7): Daraban.Modules.ServiceDesk.Services, Daraban.Modules.ServiceDesk.Data, ModelBuilder, ServiceDeskDbContext, IConfiguration, IServiceCollection, ServiceDeskModuleServiceCollectionExtensions

### Community 71 - "Community 71"
Cohesion: 0.18
Nodes (10): name, private, scripts, build, build:prod, ng, start, test (+2 more)

### Community 72 - "Community 72"
Cohesion: 0.33
Nodes (8): CancellationToken, Guid, HttpGet, IActionResult, ObjectResult, RequirePermission, Task, AssetAssignmentLookupController

### Community 73 - "Community 73"
Cohesion: 0.20
Nodes (9): compilerOptions, outDir, types, extends, files, include, src/**/*.d.ts, ./tsconfig.json (+1 more)

### Community 74 - "Community 74"
Cohesion: 0.20
Nodes (9): compilerOptions, outDir, types, extends, include, src/**/*.d.ts, ./tsconfig.json, jasmine (+1 more)

### Community 75 - "Community 75"
Cohesion: 0.22
Nodes (7): IAsyncDisposable, IConnection, SemaphoreSlim, CancellationToken, Task, ValueTask, RabbitMqConnectionProvider

### Community 76 - "Community 76"
Cohesion: 0.31
Nodes (6): IReaderRow, ITypeConverter, IWriterRow, MemberMapData, AssetStatusConverter, AssetTypeConverter

### Community 77 - "Community 77"
Cohesion: 0.20
Nodes (9): DbSet, ModelBuilder, IdentityDbContext, Entities, ProfileRights, Profiles, RefreshTokens, UserProfileEntities (+1 more)

### Community 78 - "Community 78"
Cohesion: 0.31
Nodes (6): CancellationToken, Guid, NewToken, Task, UserId, IRefreshTokenService

### Community 79 - "Community 79"
Cohesion: 0.31
Nodes (7): BackgroundService, CancellationToken, ILogger, Task, RabbitMqConsumerBackgroundService, QueueName, RoutingKey

### Community 80 - "Community 80"
Cohesion: 0.22
Nodes (9): build, builder, configurations, defaultConfiguration, production, budgets, buildTarget, fileReplacements (+1 more)

### Community 81 - "Community 81"
Cohesion: 0.25
Nodes (7): ExpiresAt, DateTimeOffset, Guid, Token, DateTimeOffset, Guid, Token

### Community 82 - "Community 82"
Cohesion: 0.39
Nodes (5): CancellationToken, Guid, IdentityDbContext, Task, RefreshTokenRepository

### Community 83 - "Community 83"
Cohesion: 0.25
Nodes (5): Daraban.Platform.ArchitectureTests, Fact, InlineData, LayeringTests, Theory

### Community 84 - "Community 84"
Cohesion: 0.25
Nodes (8): test, architect, prefix, projectType, root, sourceRoot, daraban-frontend, builder

### Community 85 - "Community 85"
Cohesion: 0.25
Nodes (7): AssetStatus, Archived, Disposed, InStock, InUse, Retired, UnderMaintenance

### Community 86 - "Community 86"
Cohesion: 0.29
Nodes (7): Error, ErrorType, BusinessRule, Conflict, Forbidden, NotFound, Validation

### Community 87 - "Community 87"
Cohesion: 0.29
Nodes (6): cli, analytics, newProjectRoot, projects, $schema, version

### Community 88 - "Community 88"
Cohesion: 0.29
Nodes (6): CancellationToken, ContentType, FileName, Guid, Stream, Task

### Community 89 - "Community 89"
Cohesion: 0.29
Nodes (4): IEventPublisher, CancellationToken, Task, RabbitMqEventPublisher

### Community 90 - "Community 90"
Cohesion: 0.29
Nodes (6): CancellationToken, ILogger, Task, InventorySubmissionConsumer, QueueName, RoutingKey

### Community 91 - "Community 91"
Cohesion: 0.29
Nodes (6): CancellationToken, ILogger, Task, QueuedNotificationConsumer, QueueName, RoutingKey

### Community 92 - "Community 92"
Cohesion: 0.29
Nodes (6): CancellationToken, ILogger, Task, RuleEvaluationConsumer, QueueName, RoutingKey

### Community 93 - "Community 93"
Cohesion: 0.47
Nodes (5): Daraban.Platform.Contracts.Assets, Guid, AssetCreatedEvent, AssetLifecycleChangedEvent, AssetUpdatedEvent

### Community 94 - "Community 94"
Cohesion: 0.33
Nodes (6): serve, proxyConfig, builder, configurations, defaultConfiguration, options

### Community 95 - "Community 95"
Cohesion: 0.33
Nodes (5): NetArchTest.Rules, Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio, Microsoft.NET.Sdk

### Community 96 - "Community 96"
Cohesion: 0.50
Nodes (4): Daraban.Platform.Contracts.ServiceDesk, Guid, TicketCreatedEvent, TicketRaisedEvent

### Community 97 - "Community 97"
Cohesion: 0.40
Nodes (5): development, buildTarget, extractLicenses, optimization, sourceMap

### Community 98 - "Community 98"
Cohesion: 0.40
Nodes (5): schematics, changeDetection, standalone, style, @schematics/angular:component

### Community 99 - "Community 99"
Cohesion: 0.50
Nodes (3): EntityTypeBuilder, AssetModelConfiguration, AssetModel

### Community 100 - "Community 100"
Cohesion: 0.50
Nodes (3): Daraban.Platform.Contracts.Inventory, Guid, RawInventoryReceivedEvent

## Knowledge Gaps
- **607 isolated node(s):** `$schema`, `version`, `newProjectRoot`, `projectType`, `style` (+602 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **17 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Result` connect `Assets Module` to `Asset Assignments API`, `Asset Categories API`, `Locations API`, `Auth Controller & Identity`, `Community 46`, `Import/Export Excel`, `Community 49`, `Community 20`, `Community 86`, `Community 88`?**
  _High betweenness centrality (0.139) - this node is a cross-community bridge._
- **Why does `AssetsDbContext` connect `Community 53` to `Community 64`, `Assets Module`, `Asset Assignments API`, `Community 99`, `Auth & Authorization`, `Locations API`, `Asset Categories API`, `Community 35`, `Asset Fields Entity`, `Community 43`, `EF Core Configuration`, `Asset Models Entity`, `Asset Entity`, `Community 23`, `Community 62`?**
  _High betweenness centrality (0.098) - this node is a cross-community bridge._
- **Why does `Asset` connect `Asset Entity` to `Assets Module`, `Asset Assignments API`, `Community 99`, `Community 35`, `Locations API`, `Asset Fields Entity`, `Community 43`, `EF Core Configuration`, `Asset Models Entity`, `Community 44`, `Community 49`, `Community 53`, `Community 85`, `Community 23`, `Community 54`, `Community 29`, `Community 62`?**
  _High betweenness centrality (0.064) - this node is a cross-community bridge._
- **What connects `$schema`, `version`, `newProjectRoot` to the rest of the system?**
  _607 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Assets Module` be split into smaller, more focused modules?**
  _Cohesion score 0.057838123787691764 - nodes in this community are weakly interconnected._
- **Should `Asset Assignments API` be split into smaller, more focused modules?**
  _Cohesion score 0.061416397296503084 - nodes in this community are weakly interconnected._
- **Should `Asset Categories API` be split into smaller, more focused modules?**
  _Cohesion score 0.06151742993848257 - nodes in this community are weakly interconnected._