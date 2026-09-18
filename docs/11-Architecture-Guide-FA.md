# راهنمای معماری — سکوی دارابان (وظیفه ۸.۵)

همراه فارسی سند انگلیسی `11-Architecture-Guide.md` و مکمل سند فارسی `01-Software-Architecture.md`، نه جایگزین آن:
سند ۰۱ نماها (منطقی، موردکاربرد، پیاده‌سازی، استقرار، فرایند)، امنیت و نیازهای غیرعملکردی را به‌تفصیل پوشش می‌دهد. این راهنما چیزی را پوشش می‌دهد که در ۰۱ نیست —
سامانه آن‌گونه که امروز *در کد* وجود دارد، ماژول‌به‌ماژول، به‌طوری‌که هر ادعا در برابر مخزن قابل بررسی است. در صورت اختلاف، این فایل در «واقعیت» مرجع است و ۰۱ در «قصد»؛ واگرایی‌های شناخته‌شده در §۸ فهرست شده‌اند.

جزئیات الزام‌آور در `docs/decisions/ADR-001` تا `ADR-007` است. این راهنما خلاصه آن‌هاست؛ رکورد اصلی همان ADRهاست.

---

## ۱. نمای کلی سامانه

```mermaid
flowchart TB
    subgraph EDGE["Edge (nginx, :80/:443)"]
        NX["nginx.prod.conf<br/>TLS 1.2+ · rate limits · gzip"]
    end

    subgraph UI["Browser SPA (Angular 21, standalone)"]
        FE["frontend:80<br/>auth · dashboard · assets · agents<br/>discovery · tickets · kb · plugins"]
    end

    subgraph HOSTS["ASP.NET Core hosts (.NET 10)"]
        API["Host.Api :8080<br/>user JWTs · 13 module APIs<br/>AgentStatusHub · TicketHub<br/>/metrics · /health/*"]
        AAPI["Host.AgentApi :8081<br/>agent JWTs (scopes) · inventory ingest<br/>AgentControlHub · /metrics · /health/*"]
    end

    subgraph MOD["14 modules (Api → Services → Data each)"]
        direction LR
        M1["Identity<br/>Assets<br/>ServiceDesk<br/>Financial"]
        M2["Knowledge<br/>Software<br/>Discovery<br/>Inventory"]
        M3["Automation*<br/>Notifications*<br/>Reporting<br/>Dashboard"]
        M4["Settings<br/>Plugins"]
    end

    subgraph WK["6 background workers (Generic Host)"]
        direction LR
        W1["CommandDispatch<br/>Discovery<br/>InventoryProcessor"]
        W2["NotificationDispatcher<br/>RuleEvaluator<br/>Reporting"]
    end

    subgraph INF["Infrastructure"]
        PG[("PostgreSQL 16<br/>15 schemas")]
        RD[("Redis 7<br/>cache + AOF/RDB")]
        RM[("RabbitMQ 3<br/>events + mgmt/prometheus")]
    end

    subgraph OBS["Observability (prod overlay)"]
        PR["Prometheus :9090"]
        GR["Grafana :3000"]
        KU["Uptime Kuma :3001"]
    end

    UI -->|HTTPS| NX
    AG["Daraban.Agent<br/>(fleet machines)"] -->|HTTPS| NX
    NX --> FE
    NX --> API
    NX --> AAPI
    API --> MOD
    AAPI --> MOD
    MOD -->|EF Core, schema-per-module| PG
    MOD -->|IDistributedCache| RD
    MOD -->|IEventPublisher| RM
    WK -->|consume| RM
    WK -->|EF Core| PG
    API -->|SignalR| UI
    AAPI -->|SignalR| AG
    API -->|/metrics| PR
    AAPI -->|/metrics| PR
    WK -->|":9102 each"| PR
    RM -->|":15692 plugin"| PR
    PR --> GR
end
```

`*` ماژول‌های `Automation` و `Notifications` ریشه‌های ترکیب ثبت‌شده با دامنه خالی هستند: `DbContext` به‌علاوه افزونه DI به‌علاوه `AssemblyMarker`، بدون انتیتی یا سرویس. این‌ها نگهدارنده استقرار هستند، نه قابلیت. هر فهرستی از «قابلیت ماژول‌ها» باید تا زمان رشد رفتار، آن‌ها را کنار بگذارد.

---

## ۲. مرزها و مسئولیت‌های ماژول‌ها

هر ماژول دقیقاً سه پروژه است — `Daraban.Modules.<Name>.{Api,Services,Data}` — به‌علاوه تست‌ها. جهت وابستگی یک‌طرفه است: `Api → Services → Data`.
`Api` هرگز به `Data` ارجاع نمی‌دهد (با `LayeringTests` اعمال می‌شود)، `Services` هرگز به `Services` یا `Data` ماژول دیگر ارجاع نمی‌دهد، و هیچ ماژولی به هاست ارجاع نمی‌دهد. ورکرها فقط به `Services`/`Data` ماژول ارجاع می‌دهند.

| ماژول | اسکیما | مالکیت | وضعیت |
|---|---|---|---|
| Identity | `identity.*` | کاربران، پروفایل‌ها/حقوق/اعطاها، JWT و چرخش refresh، قفل حساب، ایجنت‌ها و اعتبارنامه‌ها، دستورهای ایجنت، درخت انتیتی، لاگ حسابرسی | کامل |
| Assets | `assets.*` | دارایی‌ها، نوع‌ها/مدل‌ها/دسته‌ها، مکان‌ها، سازنده‌ها، تخصیص‌ها، چرخه‌عمر و تاریخچه، ایمپورت/اکسپورت، کامپیوترها | کامل |
| ServiceDesk | `servicedesk.*` | تیکت‌ها و ماشین‌وضعیت، وظایف، قالب‌ها، اعتبارسنجی‌ها، هزینه‌ها، تاریخچه، رد حسابرسی روی هر تغییر | کامل |
| Financial | `financial.*` | بودجه‌ها، قراردادها (به‌علاوه نوع‌ها/هزینه‌ها)، خریدها و اقلام، تأمین‌کننده‌ها، infocom و موتور استهلاک | کامل |
| Knowledge | `knowledge.*` | دسته‌ها (درختی)، مقاله‌ها (Draft/Published/Archived)، مخاطبان هدف، بازخورد، پیوند تیکت–راه‌حل، جست‌وجوی `tsvector` | کامل |
| Software | `software.*` | محصولات کاتالوگ، لایسنس‌ها (حسابداری صندلی)، نصب‌ها، بررسی‌های انطباق | کامل |
| Discovery | `discovery.*` | رنج‌ها، اسکن‌ها، دستگاه‌های کشف‌شده، اعتبارنامه‌های SNMP با رمزنگاری `AES-256-GCM`، قواعد دیسکاوری، قواعد ایمپورت به سبک GLPI | کامل |
| Inventory | `inventory.*` | ارسالی‌های خام ایجنت (فقط‌افزودنی)، هش‌های یکتاسازی، وضعیت پردازش | فقط دریافت — استخراج در `InventoryProcessor` است |
| Reporting | `reporting.*` | تعریف‌های گزارش، اجراها، دانلودها (`QuestPDF`/`ClosedXML`) | کامل |
| Dashboard | `dashboard.*` | کاتالوگ ویجت، چیدمان هر کاربر، اندپوینت‌های داده ویجت | کامل |
| Settings | `settings.*` و `core.*` | کاتالوگ `SystemSetting` در دیتابیس، بذردهنده راه‌اندازی، اندپوینت‌های تست اتصال | کامل |
| Plugins | `plugins.*` و `core.*` | رجیستری پلاگین، نصب/فعال/غیرفعال از zip، بارگذاری ایزوله با `AssemblyLoadContext`، اسکیمای هر پلاگین | کامل |
| Automation | `automation.*` | (خالی — فقط ریشه ترکیب) | اسکلت |
| Notifications | `notifications.*` | (خالی — فقط ریشه ترکیب) | اسکلت |

جدول‌های فراگیر در `core.*` هستند (کاتالوگ تنظیمات، رجیستری پلاگین).
هر ماژول یک `DbContext` و مهاجرت‌ها و جدول تاریخچه مهاجرت خودش را داخل اسکیمای خودش دارد — هیچ کانتکست خدای مشترکی نیست (ADR-001).

---

## ۳. الگوهای ارتباط بین ماژولی

چهار سازوکار، هر کدام دقیقاً با یک وظیفه. استفاده از هر کدام برای وظیفه دیگری نقض لایه‌بندی است، نه میان‌بر.

| سازوکار | کاربرد | نمونه | هرگز برای |
|---|---|---|---|
| فراخوانی مستقیم سرویس **داخل** یک ماژول | منطق کسب‌وکار | `TicketService` به `ITicketRepository` | فراخوانی سرویس ماژول دیگر |
| رویدادهای دامنه با `IEventPublisher` به RabbitMQ | فکت‌های بین‌ماژولی | `TicketCreatedEvent`، `AssetLifecycleChangedEvent`، `RawInventoryReceivedEvent`، `KbArticlePublishedEvent`، `ReportRequestedEvent` | درخواست/پاسخ یا هر چیزی که نیاز به جواب دارد |
| REST بین لبه و هاست‌ها | همه ورودی/خروجی خارجی | مرورگر به `Host.Api`؛ ایجنت به `Host.AgentApi` | فراخوانی هاست‌به‌هاست (هیچ‌کدام وجود ندارد) |
| هاب‌های SignalR | فقط پوش از سرور | `AgentStatusHub` و `TicketHub` (از `Host.Api` به مرورگر)؛ `AgentControlHub` (از `AgentApi` به ایجنت) | فراخوانی به سبک RPC؛ وضعیت در Postgres است |

قراردادهای رویداد در `Daraban.Platform.Contracts` هستند (فضانام‌های `Agents`، `Assets`، `Inventory`، `Knowledge`، `Reporting`، `ServiceDesk`) و *تنها* ارجاع‌های بین‌ماژولی هستند که تست‌های معماری اجازه می‌دهند. کاتالوگ کامل با شکل بار:

| رویداد | ناشر | معنا |
|---|---|---|
| `AgentRegisteredEvent` / `AgentDeactivatedEvent` / `AgentCredentialRevokedEvent` | Identity | تغییرهای عضویت ناوگان ایجنت |
| `AgentCommandPublishedEvent` | Identity | یک دستور وارد صف شد (ورکر آن را اعزام می‌کند) |
| `AgentCommandCompletedEvent` / `AgentCommandTimedOutEvent` | ورکر | نتیجه‌های پایانی دستور |
| `AssetCreatedEvent` / `AssetUpdatedEvent` / `AssetLifecycleChangedEvent` | Assets | فکت‌های دارایی که ماژول‌های دیگر به آن واکنش می‌دهند |
| `RawInventoryReceivedEvent` | Inventory | یک ارسال خام ذخیره شد و آماده استخراج است |
| `TicketCreatedEvent` / `TicketRaisedEvent` | ServiceDesk | کار جدید وارد صف شد |
| `KbArticlePublishedEvent` / `KbArticleUnpublishedEvent` / `KbArticleLinkedToTicketEvent` | Knowledge | تغییرهای رؤیت‌پذیری KB؛ سرویس‌دسک راه‌حل را از رویداد پیوند مُهر می‌کند |
| `ReportRequestedEvent` | Reporting | تولید دستی گزارش (مصرف‌کننده: ورکر Reporting) |

---

## ۴. هاست‌ها، ورکرها و کتابخانه‌های مشترک

**Host.Api (پورت داخلی ۸۰۸۰)** — هاست کاربرمحور. هر ۱۴ ماژول را ثبت می‌کند، ۱۳ API ماژولی را سرو می‌کند (Inventory کنترلر مرورگرمحور ندارد؛ دریافت متعلق به AgentApi است)، هر دو هاب مرورگر، اعتبارسنجی JWT با `token_version`، محدودکننده‌های نرخ `auth` و `discovery-scan`، و `/metrics`.

**Host.AgentApi (پورت داخلی ۸۰۸۱)** — هاست ماشین‌محور. فقط Identity و Inventory را ثبت می‌کند، احراز هویت ایجنت (`client_credentials`، بدون refresh token)، دریافت اینونتوری، تأیید/نتیجه دستور، مدیریت ایجنت (ادمین‌های انسانی با JWT کاربر)، و `AgentControlHub` را سرو می‌کند. مجوزدهی مبتنی‌بر scope با `agent:scope:*`، نه مجوزهای RBAC.

**ورکرها** (همه مصرف‌کننده `IHostedService`، همه `:9102/metrics` را اکسپوز می‌کنند):

| ورکر | مصرف می‌کند | انجام می‌دهد |
|---|---|---|
| CommandDispatch | صف دستور | پوش با SignalR، اعمال تایم‌اوت هر نوع، تلاش مجدد |
| Discovery | زمان‌بندی‌ها | اسکن ICMP/ARP/SNMP هر رنج |
| InventoryProcessor | `RawInventoryReceivedEvent` | استخراج دستگاه‌های ساخت‌یافته از ارسالی‌های خام |
| NotificationDispatcher | رویدادهای اعلان | توزیع fan-out |
| RuleEvaluator | رویدادهای دامنه | ارزیابی قواعد کسب‌وکار |
| Reporting | `ReportRequestedEvent` | تولید فایل‌ها در فروشگاه مشترک گزارش |

**کتابخانه‌های مشترک** (`src/Shared/*`): ‏`Common` شامل `Result<T>`، ‏`Error`، ‏`BaseEntity`/`TenantScopedEntity`، ‏`PagedList`، ‏`Guard`؛ ‏`Abstractions` شامل `ICurrentUser`، ‏`IEventPublisher`، ‏`JwtOptions` و غیره؛ ‏`Contracts` (کاتالوگ رویداد بالا)؛ ‏`Hosting` شامل Serilog، ‏ProblemDetails، هلث‌چک‌ها، انتخاب کش توزیع‌شده، فشرده‌سازی پاسخ، لوله‌کشی سیاست احراز هویت، اکسپوزیشن Prometheus — یک مالک برای هر سیاست طبق ADR-002؛ ‏`Messaging` (زیرساخت ناشر/مصرف‌کننده با `RabbitMQ.Client` خالص، بدون MassTransit).

---

## ۵. تصمیم‌های کلیدی معماری و دلیل آن‌ها

| تصمیم | انتخاب | چرا (تک‌بند) |
|---|---|---|
| شکل کلی | مونولیت ماژولار، سه‌لایه کلاسیک هر ماژول | تکامل مستقل بدون هزینه عملیاتی service-mesh (ADR-001) |
| الگوهای ممنوعه | بدون الگوهای تاکتیکی DDD، بدون CQRS، بدون MediatR | فراخوانی‌های مستقیم قابل ردیابی‌اند؛ تشریفات بدون مقیاس هزینه است |
| ORM / دیتابیس | EF Core 10 با Npgsql، ‏PostgreSQL 16، اسکیما‌به‌ازای‌ماژول | یک موتور، مرزهای سخت، مهاجرت هر ماژول جدا |
| کلیدهای اصلی | همه‌جا UUIDv7 | مرتب‌زمانی و سازگار با ایندکس؛ تصادفی‌بودن v4 کلیدهای خوشه‌ای را تکه‌تکه می‌کند |
| ترافیک بین‌ماژولی | فقط رویدادهای Contracts روی RabbitMQ.Client | سطح کوپلینگ صریح؛ اجتناب از لایسنس MassTransit |
| احراز هویت کاربر | JWT دست‌ساز RS256 (۱۵ دقیقه) و refresh token مات چرخشی | نشست‌های قابل ابطال بدون denylist؛ ‏`token_version` توکن‌ها را فوراً می‌کشد |
| احراز هویت ایجنت | OAuth2 از نوع client credentials، مبتنی‌بر scope، بدون refresh | ماشین‌ها دوباره احراز هویت می‌کنند؛ scopeها هنگام صدور گره می‌خورند |
| مجوزدهی | مجوزهای پویای `module.action` با کش Redis | مجموعه مجوز باز بدون سیاست‌های پیش‌ثبت‌شده |
| وضعیت فرانت‌اند | Signal Store در `@ngrx/signals`، استخراج خطای مشترک | بدون بویلرپلیت NgRx کلاسیک؛ یک سیاست خطا (ADR-006) |
| خطاها | `Result<T>` و یک نگاشت واحد `ToProblemResult` | ۲۲ کپی واگرا نیاز را ثابت کرد (ADR-003) |
| پلاگین‌ها | `AssemblyLoadContext` ایزوله، اسکیمای هر پلاگین | کد شخص‌ثالث قابل unload و با کمترین امتیاز (ADR-007) |
| جست‌وجوی تمام‌متن | ستون تولیدشده `tsvector` در Postgres و GIN | بدون وابستگی به Elasticsearch برای جست‌وجوی KB |
| سکرت‌ها | اعتبار SNMP با AES-256-GCM؛ کلید JWT از فایل PEM؛ گذرواژه بکاپ از فایل root-only | شعاع انفجار هر سکرت یک زیرسیستم است |
| نسخه‌ها | `Directory.Packages.props` مرکزی؛ .NET 10؛ ‏Angular 21 | یک نقطه برای ارتقا، بازیابی‌های تکرارپذیر |

در موارد خلاصه‌سازی این جدول، ADRهای ۰۰۱ تا ۰۰۷ در `docs/decisions/` الزام‌آورند.

---

## ۶. معماری فرانت‌اند (نقشه، نه دفترچه)

Angular 21، کامپوننت‌های standalone، روت‌های feature با بارگذاری تنبل پشت `authGuard`/`guestGuard`؛ یک Signal Store برای هر فیچر (`auth`، ‏`assets`، ‏`agents`، ‏`discovery` و غیره)؛ JWT در حافظه، refresh با کوکی HttpOnly؛ اینترسپتور Bearer را می‌چسباند و روی ۴۰۱ خودش refresh می‌کند؛ کلاینت‌های SignalR برای دو هاب مرورگر. نگاشت فیچر‌به‌روت API در `12-API-Reference.md` پین شده است (شکست قراردادی `/api/v1/auth` به `/api/v1/identity/auth` در وظیفه ۲.۵ رفع شده و نباید برگردد).

## ۷. نمای استقرار (نقشه، نه دفترچه)

کامپوز Docker تک‌هاست توپولوژی پشتیبانی‌شده است: ‏nginx (پورت‌های ۸۰/۴۴۳) به هاست‌ها/فرانت‌اند؛ Postgres/Redis/RabbitMQ داخلی؛ ‏`docker-compose.prod.yml` لایه TLS، پشته مانیتورینگ و ماندگاری سخت‌شده را اضافه می‌کند. رویه‌ها در `09-Deployment-Guide.md`؛ عملیات روز دوم در `10-Backup-Restore-Runbook.md` و `15-Operations-Guide.md`.

## ۸. واگرایی مستنداتی شناخته‌شده (عمداً ویرایش نشده)

- سند `01-Software-Architecture.md` در §۳/§۶ پنج ورکر می‌گوید (شش‌تا هستند)، ‏`KbArticle.IsPublished: bool` را نشان می‌دهد (اکنون enum وضعیت است)، ‏`Budget.FiscalYear` (اکنون Start/EndDate)، ‏`SoftwareLicense.TotalSeats/UsedSeats` (اکنون Quantity/UsedQuantity)، و §۶-۱ مسیریابی قدیمی stripping با `/agent/` را مستند کرده که در وظیفه ۸.۴ جایگزین شد. سند ۰۱ فارسی است؛ اصلاح آن تصمیم مالک است، نه این وظیفه.
- سند `06-Not-Implemented.md` در §۵ (مهاجرت‌های یکپارچه معوق) همچنان دقیق است و در `13-Database-Guide.md` ارجاع شده — نه متناقض با آن.
