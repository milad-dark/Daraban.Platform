# راهنمای توسعه‌دهنده — سکوی دارابان (وظیفه ۸.۵)

روش کار در این مخزن: راه‌اندازی، دستورالعمل ماژول، مجوزها، پلاگین‌ها و تست‌ها. قراردادهای زیر هر جا گفته شده با بیلد یا تست اعمال می‌شوند؛ بقیه با بازبینی کد اعمال می‌شوند. وقتی شک کردید، از ماژول Knowledge تقلید کنید — جدیدترین برش عمودی کامل است (انتیتی‌ها تا مخزن‌ها تا سرویس‌ها تا کنترلرها تا تست‌ها تا مهاجرت).

---

## ۱. راه‌اندازی پروژه

**پیش‌نیازها:** ‏.NET 10 SDK (خروجی `dotnet --version` باید ۱۰.x باشد)، ‏Node 22 برای `frontend/`، ‏Docker Engine با پلاگین Compose (برای استک و تست‌های یکپارچه)، ‏Git. نیازی به PostgreSQL/Redis/RabbitMQ لوکال نیست — استک compose آن‌ها را می‌دهد.

```bash
git clone https://github.com/milad-dark/Daraban.Platform && cd Daraban.Platform
cp .env.example .env          # سپس گذرواژه‌های واقعی و DOCKERHUB_USERNAME را ست کنید
mkdir -p certs && openssl genrsa -out certs/jwt-signing-key.pem 3072 && chmod 600 certs/jwt-signing-key.pem
docker compose up -d          # شامل postgres و redis و rabbitmq و هاست‌ها و ورکرها و فرانت‌اند و nginx
```

بک‌اند (برای کار روزمره نیازی به Docker نیست):

```bash
dotnet build Daraban.Platform.sln
dotnet test Daraban.Platform.sln -c Release --filter "FullyQualifiedName!~Daraban.IntegrationTests"
```

فرانت‌اند:

```bash
cd frontend && npm ci --legacy-peer-deps && npx ng serve   # روی http://localhost:4200
```

نکته‌های اولین اجرا: اسکیمای دیتابیس به‌صورت خودکار bootstrap **نمی‌شود** (بخش ۹ سند `13-Database-Guide.md` و سند ۰۹ بخش ۶.۱ گام ۸ را ببینید). در Development هاست‌ها کلید JWT موقت را می‌پذیرند؛ در Production نبود `Jwt__SigningKeyPemPath` عمداً کرش راه‌اندازی است.

---

## ۲. افزودن ماژول جدید (دستورالعمل)

ماژول یعنی سه پروژه به‌علاوه تست‌ها که دقیقاً در پنج نقطه سیم‌کشی می‌شوند. رد کردن یک گام خرابی خاموش می‌دهد (سرویس ثبت‌نشده فقط وقتی صدا زده شود throw می‌کند؛ کنترلر فهرست‌نشده ۴۰۴ می‌دهد؛ پروژه سیم‌کشی‌نشده هرگز کامپایل نمی‌شود — ماژول Software هر سه خرابی را یک‌جا در PR شماره ۲۳ داشت که از آن زمان رفع شده است).

**گام ۱ — ساخت پروژه‌ها** (نام‌ها بار معنایی دارند؛ DI و تست‌ها و مستندات همه آن‌ها را فرض می‌کنند):

```
src/Modules/<Name>/Daraban.Modules.<Name>.Api/          # کنترلرها و AssemblyMarker.cs
src/Modules/<Name>/Daraban.Modules.<Name>.Services/     # سرویس‌ها، DTOها، ولیدیتورها، افزونه DI
src/Modules/<Name>/Daraban.Modules.<Name>.Data/         # انتیتی‌ها، Configurations/، مخزن‌ها، DbContext
tests/UnitTests/Daraban.Modules.<Name>.Tests/          # با xunit و Moq
```

پروژه Api از `Sdk="Microsoft.NET.Sdk"` استفاده می‌کند (نه `Sdk.Web`) به‌علاوه `FrameworkReference` به `Microsoft.AspNetCore.App`، و به Services و `Daraban.Platform.Hosting` و `Daraban.Platform.Abstractions` ارجاع می‌دهد. هر `*.Api.csproj` موجود را کپی کنید — دستی ننویسید (Api در Software با مسیر نسبی دو سطح کوتاه و `Sdk.Web` آمد و هرگز کامپایل نشد).

**گام ۲ — انتیتی و نوع پایه‌اش.** کم‌قدرت‌ترین پایه‌ای که می‌خورد را بردارید: `BaseEntity` (فقط ستون‌های حسابرسی)، ‏`SoftDeletableEntity` (به‌علاوه `is_deleted`)، ‏`TenantScopedEntity` (به‌علاوه `entity_id`). شناسه‌های جدید در سرویس با `Guid.CreateVersion7()` ساخته می‌شوند — هرگز `Guid.NewGuid()` و هرگز تولید دیتابیسی. ‏Enumها به‌صورت رشته persist می‌شوند (`HasConversion<string>`).
ارجاع‌های بین‌ماژولی `Guid`های لخت **بدون FK** هستند (precedent در KB با `KbTicketLink.TicketId` و `KbArticleTarget.TargetId`) — کلید خارجی بین اسکیماها نقض لایه‌بندی است، نه یکپارچگی.

**گام ۳ — اینترفیس مخزن و پیاده‌سازی** در Data (روی اینترفیس `Add/Update` و `SaveChangesAsync` مثل هر ماژول دیگر — مالک مرز تراکنش سرویس است، پس `SaveChangesAsync` باید جدا از `AddAsync` قابل فراخوانی باشد). بررسی‌های یکتایی نام شکل `(name, entityId, excludeId)` می‌گیرند؛ الگوی nullable-Guid در `TicketTemplateRepository` را مقایسه کنید (یک `t.Id != excludeId` لخت فقط به تصادف معناشناسی null در SQL کار می‌کند — صریح باشید).

**گام ۴ — سرویس با خروجی `Result<T>`** که برای خرابی‌های موردانتظار (اعتبارسنجی، یافت‌نشدن، قواعد کسب‌وکار) هرگز throw نمی‌کند. صفحه‌بندی را ورودی اعتبارسنجی کنید (`NormalizePaging`: صفحه بزرگ‌ترمساوی ۱، اندازه clamp بین ۱ تا ۲۰۰، پیش‌فرض ۲۰ — یک `page=0` بدون clamp تولید `Skip(-pageSize)` می‌کند و Postgres ردش می‌کند؛ هر ماژول این را جدا یاد گرفت). سطرهای حسابرسی/تاریخچه را در همان `SaveChangesAsync` تغییر بنویسید.

**گام ۵ — سیم‌کشی پنج نقطه:**

۱. افزونه `Daraban.Modules.<Name>.Services.<Name>ModuleServiceCollectionExtensions.Add<Name>Module` — شامل DbContext (با `MigrationsHistoryTable("__EFMigrationsHistory", "<schema>")`)، ولیدیتورها، مخزن‌ها و سرویس‌ها. یک ریشه ترکیب برای هر ماژول (ADR-005).
۲. ‏`Program.cs` هر دو هاست: `Add<Name>Module(...)` و `AddApplicationPart` (بدون دومی کنترلرها ۴۰۴ می‌دهند — ‏ASP.NET خودش آن‌ها را کشف نمی‌کند).
۳. فایل `Daraban.Platform.sln` (به‌علاوه پوشه solution و ورودی‌های config و nesting) یا `dotnet sln add`.
۴. ارجاع `Host.Api` به پروژه‌های Api و Data (ارجاع `AgentApi` فقط اگر ایجنت‌ها به آن دست بزنند).
۵. ‏`RequirePermission("<module>.<action>")` روی هر اکشن (بخش ۳).

**گام ۶ — تست‌ها.** پروژه‌های تست خالی `.csproj` بیلد را با نویز «no tests» خراب می‌کنند؛ تست واقعی بنویسید: رفتار سرویس با مخزن‌های mock (mockهای strict — فراخوانی پیش‌بینی‌نشده باید fail بدهد، نه مقدار پیش‌فرض برگرداند)، به‌علاوه تست مدل اگر ماژول نگاشت غیربدیهی دارد (ستون‌های تولیدشده، ایندکس‌های یکتای جزئی، فیلترهای کوئری). ساختار `Daraban.Modules.Knowledge.Tests` را آینه کنید.

**گام ۷ — مهاجرت و کارخانه design-time.** ‏`KnowledgeDbContextFactory` را کپی کنید (یک `IDesignTimeDbContextFactory` تا مهاجرت‌ها بدون بوت هاست تولید شوند) و مهاجرت اولیه ماژول را تولید کنید (بخش ۹ راهنمای دیتابیس).

---

## ۳. افزودن مجوز جدید

مجوزها رشته‌های باز (`module.action`) هستند که هر درخواست از `UserProfileEntity` به `Profile` به `ProfileRight` resolve و در Redis کش می‌شوند. افزودن یکی سه گام دارد و گام ۲ فعلاً UI ندارد:

۱. روی اکشن `[RequirePermission("<module>.<action>")]` بگذارید. دانه‌بندی موجود را نگه دارید — read/write/delete، به‌علاوه فعل متمایز فقط وقتی عمل مادی متفاوت است (`knowledge.publish`، ‏`software.delete`).
گذارهای gated با انتشار مجوز خودشان را می‌گیرند، نه فیلدی که داخل DTO ویرایش چرخانده شود.
۲. اعطا را مستقیم در دیتابیس درج کنید (**هنوز هیچ API یا UI ادمین** برای profiles/rights نیست — ‏Users/Auth/AuditLogs تنها کنترلرهای Identity هستند):
   ```sql
   INSERT INTO identity.profile_rights (id, profile_id, module, action, is_recursive)
   VALUES (gen_random_uuid(), '<profile-id>', '<module>', '<action>', true);
   INSERT INTO identity.user_profile_entities (id, user_id, profile_id, entity_id, is_recursive, is_default)
   VALUES (gen_random_uuid(), '<user-id>', '<profile-id>', '<entity-id>', true, true);
   ```
۳. کش مجوز کاربر و انتیتی اثرگرفته را باطل کنید (`IPermissionResolver.InvalidateAsync`) یا TTL پنج‌دقیقه‌ای را صبر کنید.

مثل `PermissionEnforcementTests` تستش کنید: همان اندپوینت، کاربر دارای اعطا ۲۰۰ می‌گیرد و کاربر بدون اعطا ۴۰۳ — هرگز تفاوت ۴۰۴ در برابر ۴۰۳ که وجود مجوز را لو بدهد.

---

## ۴. نوشتن پلاگین

پلاگین‌ها پلتفرم را بعد از استقرار و بدون بازسازی هاست گسترش می‌دهند (ADR-007). قرارداد در `src/Shared/Daraban.Platform.Plugins` است:

```csharp
public interface IPlugin
{
    string Id { get; }        // باید با شناسه مانیفست بخورد
    string Name { get; }
    string Version { get; }   // باید با نسخه مانیفست بخورد
    void ConfigureServices(IServiceCollection services);  // کالکشن ایزوله
    void ConfigureApp(IApplicationBuilder app);           // اندپوینت‌ها/میدل‌ویرها
    IEnumerable<PluginMenuItem> GetMenuItems();           // ورودی‌های ناوبری Angular
}
```

چیدمان بسته (zip): اسمبلی و `manifest.json` (فیلدهای `id`، ‏`name`، ‏`version`، ‏`entryPoint`، ‏`assemblyFile`، ‏`author`، ‏`minimumHostVersion` و `remoteEntryUrl` اختیاری) و مهاجرت‌های SQL.
آپلود با `POST /api/v1/plugins` (فقط ادمین)؛ فعال/غیرفعال/حذف با اندپوینت‌های متناظر؛ هر پلاگین اسکیمای خودش `plugins_<id>.*` را می‌گیرد که با `IPluginMigration` اجرا می‌شود.

قاعده‌هایی که بار معنایی دارند، نه سبکی:

- دسترسی به دیتابیس **فقط** از `IPluginDbContext` — ‏`DbContext` هاست و connection string هرگز به کد پلاگین داده نمی‌شود.
- اسمبلی‌ها در `AssemblyLoadContext` ایزوله قابل‌جمع‌آوری بار می‌شوند (`PluginLoadContext`)؛ پین‌کردن با `Assembly.LoadFrom` یعنی نشت حافظه در هر نصب مجدد.
- مسیر آپلود سطح zip-slip/zip-bomb است: `PluginPackageManager` مسیرها و اندازه‌های ورودی را اعتبارسنجی می‌کند — با استخراج‌کننده دوم دورش نزنید.
- آیتم‌های منو داده‌اند (`PluginMenuItemDto`) که شل رندر می‌کند؛ پلاگین هرگز کد Angular به باندل هاست تزریق نمی‌کند.

---

## ۵. اجرای تست‌ها

| دستور | چه چیزی اجرا می‌شود | نکته‌ها |
|---|---|---|
| `dotnet test Daraban.Platform.sln -c Release --filter "FullyQualifiedName!~Daraban.IntegrationTests"` | همه تست‌های واحد (۱۰۸۲ عدد در وظیفه ۸.۵) | دقیقاً آینه CI با `ci.yml` |
| `dotnet test tests/UnitTests/Daraban.Modules.<X>.Tests` | یک ماژول | حلقه بازخورد سریع |
| `dotnet test tests/IntegrationTests/... --filter "FullyQualifiedName~Daraban.IntegrationTests"` | سوئیت Testcontainers با Postgres و Redis و RabbitMQ | نیاز به Docker daemon دارد؛ از مرحله واحد کنار گذاشته شده است |
| `docker build --target test -f docker/host-api.Dockerfile .` | تست‌های واحد داخل Docker | همان چیزی که CI واقعاً اجرا می‌کند |

قراردادهایی که سوئیت را صادق نگه می‌دارند: رفتار strict در Moq همه‌جا (فراخوانی arrange‌نشده تست را fail می‌کند به‌جای برگرداندن پیش‌فرض)؛ بدون `Thread.Sleep` (از primitiveهای async استفاده کنید)؛ زمان تزریق یا فریز می‌شود، هرگز `DateTimeOffset.UtcNow` دقیق assert نمی‌شود؛ تست‌های وابسته به Docker فقط در پروژه IntegrationTests زندگی می‌کنند، هرگز در پروژه‌های واحد. ژنراتورهای مستندات که باید صادق بمانند کنار مستنداتی هستند که تغذیه می‌کنند: `docs/gen-endpoints.ps1` (موجودی API) و `docs/gen-db.ps1` (موجودی اسکیما).
