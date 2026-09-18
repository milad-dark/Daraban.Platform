# مرجع API — سکوی دارابان (وظیفه ۸.۵)

سطح کامل HTTP هر دو هاست، تولیدشده از سورس در ۱۶-۰۹-۲۰۲۶ (رویه تازه‌سازی در §۱۶). دو هاست، دو مخاطب:

| هاست | پورت (داخلی) | سرو می‌دهد | مدل احراز هویت |
|---|---|---|---|
| `Host.Api` | ۸۰۸۰ | SPA مرورگر: ۱۳ API ماژولی و ۲ هاب SignalR | ‏JWT کاربر (Bearer) و مجوزهای RBAC به شکل `module.action` |
| `Host.AgentApi` | ۸۰۸۱ | ماشین‌های ناوگان: احراز هویت، دریافت اینونتوری، دستورها، مدیریت ایجنت | ‏JWT ایجنت با scope؛ اندپوینت‌های ادمین JWT کاربر می‌گیرند |

از پشت لبه (nginx)، ترافیک مرورگر زیر `/api/...` و ترافیک ایجنت هم زیر `/api/...` می‌رسد — مسیرهای کامل کنترلر در ادامه توسط پروکسی عیناً حفظ می‌شوند (اگر رفتار قدیمی stripping را یادتان هست، §۸ سند `11-Architecture-Guide.md` را ببینید). دسترسی مستقیم به کانتینر هم همان مسیرهاست.

راهنمای جدول‌ها: **Auth** یعنی `[Authorize]` (نیازمند JWT)؛ نام مجوز یعنی مقدار `[RequirePermission]` که اعطای فراخواننده باید داشته باشد؛ `Anonymous` یعنی بدون احراز هویت — دقیقاً شش اندپوینت: login/register/refresh/logout، اندپوینت توکن ایجنت، و دست‌تکان `prolog` پیش‌از‌احراز ایجنت (§۹).

---

## ۱. هاست Api — هویت و دسترسی

| متد | مسیر | احراز هویت |
|---|---|---|
| POST | `/api/v1/identity/auth/register` | Anonymous |
| POST | `/api/v1/identity/auth/login` | Anonymous |
| POST | `/api/v1/identity/auth/refresh` | Anonymous (کوکی HttpOnly) |
| POST | `/api/v1/identity/auth/logout` | Anonymous (کوکی را پاک می‌کند) |
| GET | `/api/v1/identity/users?entityId&q&page&pageSize` | `identity.users.read` |
| GET | `/api/v1/identity/users/{id}` | `identity.users.read` |
| POST | `/api/v1/identity/users` | `identity.users.write` |
| PUT | `/api/v1/identity/users/{id}` | `identity.users.write` |
| POST | `/api/v1/identity/users/{id}/active` | `identity.users.write` |
| DELETE | `/api/v1/identity/users/{id}` | `identity.users.delete` |
| GET | `/api/v1/audit-logs?entity/actor/from/to&page&pageSize` | `identity.auditlogs.read` |
| GET | `/api/v1/audit-logs/{entityType}/{entityId}` | `identity.auditlogs.read` |

## ۲. هاست Api — دارایی‌ها

| متد | مسیر | احراز هویت |
|---|---|---|
| GET | `/api/v1/assets?status&assetTypeId&locationId&search&page&pageSize` | `assets.read` |
| POST | `/api/v1/assets` | `assets.write` |
| GET | `/api/v1/assets/{id}` | `assets.read` |
| PUT | `/api/v1/assets/{id}` | `assets.write` |
| DELETE | `/api/v1/assets/{id}` | `assets.delete` |
| GET | `/api/v1/assets/export?format=csv\|xlsx&…` | `assets.read` |
| POST | `/api/v1/assets/import?dryRun` به‌صورت multipart | `assets.write` |
| GET | `/api/v1/assets/import/template` | `assets.read` |
| GET/POST | `/api/v1/asset-types` و `/api/v1/asset-types/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/asset-categories` و `/api/v1/asset-categories/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/locations` و `/api/v1/locations/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/manufacturers` و `/api/v1/manufacturers/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/assets/{assetId}/assignments` | `assets.read` / `assets.write` |
| GET/DELETE | `/api/v1/assets/{assetId}/assignments/current` | `assets.read` / `assets.write` |
| GET | `/api/v1/users/{userId}/assets` و `/api/v1/departments/{departmentId}/assets` | `assets.read` |
| POST | `/api/v1/assets/{assetId}/lifecycle/transition` | `assets.write` |
| GET | `/api/v1/assets/{assetId}/lifecycle/history` | `assets.read` |

## ۳. هاست Api — سرویس‌دسک

| متد | مسیر | احراز هویت |
|---|---|---|
| GET | `/api/v1/tickets?type&status&priority&assignedUserId&assignedGroupId&search&page&pageSize` | `servicedesk.read` |
| POST | `/api/v1/tickets` | `servicedesk.write` |
| GET | `/api/v1/tickets/{id}` | `servicedesk.read` |
| PUT | `/api/v1/tickets/{id}` | `servicedesk.write` |
| DELETE | `/api/v1/tickets/{id}` (فقط New/Cancelled) | `servicedesk.delete` |
| PUT | `/api/v1/tickets/{id}/status` | `servicedesk.write` |
| PUT | `/api/v1/tickets/{id}/assign` | `servicedesk.write` |
| PUT | `/api/v1/tickets/{id}/escalate` | `servicedesk.write` |
| PUT | `/api/v1/tickets/{id}/solve` (متن راه‌حل الزامی) | `servicedesk.write` |
| PUT | `/api/v1/tickets/{id}/close` (فقط Solved) | `servicedesk.write` |
| GET | `/api/v1/tickets/{id}/history` | `servicedesk.read` |
| GET | `/api/v1/tickets/count/open` و `/api/v1/tickets/count/overdue` | `servicedesk.read` |
| GET/POST | `/api/v1/tickets/{ticketId}/tasks` | read / write |
| DELETE | `/api/v1/tickets/{ticketId}/tasks/{id}` (فقط وظیفه‌های خود شخص) | `servicedesk.write` |
| GET/POST | `/api/v1/ticket-templates` و `/api/v1/ticket-templates/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |

## ۴. هاست Api — مالی

نکته مسیریابی: این پنج کنترلر از `[Route("api/[controller]")]` استفاده می‌کنند، پس مسیرها جمع PascalCase کنترلر را حفظ می‌کنند (`/api/Budgets`، نه `/api/budgets`). هر ماژول دیگر از kebab-case کوچک استفاده می‌کند — این ناسازگاری واقعی است، این‌جا مستند شده و نامزد تغییرنام است (کلاینت‌های موجود را می‌شکند، پس به مهاجرت نسخه‌دار نیاز دارد، نه اصلاح خاموش).

| کنترلر | اکشن‌های beyond CRUD | احراز هویت |
|---|---|---|
| `/api/Budgets` | ‏`GET /summary` (جمع کل بودجه‌ها) | `financial.read` / write / delete |
| `/api/Contracts` | ‏`POST /{id}/status` با چرخه Draft به Active به Suspended/Expired/Cancelled | `financial.read` / write / delete |
| `/api/Purchases` | ‏`POST /{id}/status`، ‏`POST /{id}/items`، ‏`DELETE /{id}/items/{itemId}` | `financial.read` / write / delete |
| `/api/Suppliers` | — | `financial.read` / write / delete |
| `/api/Infocoms` | ‏`GET /asset/{assetId}`، ‏`GET /{id}/depreciation` | `financial.read` / write / delete |

هر پنج‌تا `GET` صفحه‌دار با فیلتر، `GET /{id}`، ‏`POST`، ‏`PUT /{id}` و `DELETE /{id}` را با مجوز متناظر `financial.*` پشتیبانی می‌کنند.

## ۵. هاست Api — دانش

| متد | مسیر | احراز هویت |
|---|---|---|
| GET | `/api/v1/kb/articles?categoryId&status&isFaq&authorUserId&title&page&pageSize` | `knowledge.read` |
| GET | `/api/v1/kb/articles/search?q&categoryId&status&page&pageSize` با ترتیب `ts_rank` | `knowledge.read` |
| GET | `/api/v1/kb/articles/{id}?countView=` | `knowledge.read` |
| POST | `/api/v1/kb/articles` (همیشه Draft متولد می‌شود) | `knowledge.write` |
| PUT | `/api/v1/kb/articles/{id}` (وقتی Archived نیست) | `knowledge.write` |
| POST | `/api/v1/kb/articles/{id}/status` با چرخه Draft به Published به Archived | `knowledge.publish` |
| DELETE | `/api/v1/kb/articles/{id}` | `knowledge.delete` |
| POST | `/api/v1/kb/articles/{id}/feedback` (هر خواننده‌ای می‌تواند امتیاز بدهد) | `knowledge.read` |
| GET | `/api/v1/kb/articles/{id}/feedback` | `knowledge.write` |
| GET/POST | `/api/v1/kb/categories[?tree=true]` و `/api/v1/kb/categories/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET | `/api/v1/tickets/{ticketId}/kb-links` | `servicedesk.read` |
| POST | `/api/v1/tickets/{ticketId}/solution` | `servicedesk.write` |
| DELETE | `/api/v1/tickets/{ticketId}/kb-links/{articleId}` | `servicedesk.write` |

## ۶. هاست Api — نرم‌افزار

| متد | مسیر | احراز هویت |
|---|---|---|
| GET/POST | `/api/v1/softwares` و `/api/v1/softwares/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/software-licenses` و `/api/v1/software-licenses/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET | `/api/v1/software-licenses/{id}/compliance` | `software.read` |
| GET | `/api/v1/software-licenses/software/{softwareId}` | `software.read` |
| GET/POST | `/api/v1/software-installations` و `/api/v1/software-installations/{id}` | read / write |
| POST | `/api/v1/software-installations/{id}/uninstall` | `software.write` |
| GET | `/api/v1/software-installations/asset/{assetId}` (به‌علاوه `/summary`) | `software.read` |
| GET | `/api/v1/software-installations/software/{softwareId}` | `software.read` |

## ۷. هاست Api — دیسکاوری

| متد | مسیر | احراز هویت |
|---|---|---|
| GET/POST | `/api/v1/discovery/ranges` و `/api/v1/discovery/ranges/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| POST | `/api/v1/discovery/ranges/{id}/scan` | `discovery.write` به‌علاوه سیاست نرخ `discovery-scan` |
| GET | `/api/v1/discovery/scans/recent`، ‏`/scans/range/{rangeId}`، ‏`/scans/{id}`، ‏`/scans/{id}/devices` | `discovery.read` |
| GET | `/api/v1/discovery/devices/recent` و `/ranges/{rangeId}/devices` | `discovery.read` |
| GET/POST | `/api/v1/discovery/credentials` و `/credentials/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/discovery/rules` و `/rules/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/discovery/import-rules` و `/import-rules/{id}` (به‌علاوه PUT/DELETE) | read / write / delete |
| GET | `/api/v1/discovery/import-rules/active` | `discovery.read` |
| POST | `/api/v1/discovery/import-rules/evaluate` | `discovery.write` |
| GET | `/api/v1/discovery/import-rules/fields`، ‏`/operators`، ‏`/action-types` | `discovery.read` |
| GET | `/api/v1/discovery/dashboard` | `discovery.read` |

## ۸. هاست Api — داشبورد / تنظیمات / پلاگین‌ها / گزارش‌ها

| متد | مسیر | احراز هویت |
|---|---|---|
| GET | `/api/v1/dashboard/widgets` | `dashboard.read` |
| GET/PUT | `/api/v1/dashboard/layout` | read / `dashboard.write` |
| GET | `/api/v1/dashboard/data/{widgetType}` | `dashboard.read` |
| GET | `/api/v1/settings` | `settings.read` |
| PUT | `/api/v1/settings/{key}` | `settings.write` |
| POST | `/api/v1/settings/test/{key}` | `settings.write` |
| GET/POST | `/api/v1/plugins` و `/api/v1/plugins/{pluginId}` (به‌علاوه DELETE) | read / write |
| POST | `/api/v1/plugins/{pluginId}/enable` و `.../disable` | `plugins.write` |
| GET | `/api/v1/plugins/menu-items` | `plugins.read` |
| GET | `/api/v1/reports/definitions` و `/api/v1/reports/my` | `reports.read` |
| POST | `/api/v1/reports/definitions` | `reports.manage` |
| POST | `/api/v1/reports/{id}/generate` | `reports.manage` |
| GET | `/api/v1/reports/{id}/runs` و `/api/v1/reports/{id}/download` | `reports.read` |
| GET | `/api/v1/agents`، ‏`/api/v1/agents/{id}` و `/summary` (نمای ادمین ناوگان) | `Auth` (به‌علاوه اعطاهای ادمین) |
| GET | `/api/v1/agents/{id}/inventory` و `/jobs` | `Auth` (به‌علاوه اعطاهای ادمین) |

## ۹. هاست AgentApi — سطح ماشین

اندپوینت توکن عمداً ناشناس است (نمی‌شود برای گرفتن توکن، توکن ارائه کرد)؛ بقیه به JWT ایجنت با scope نام‌برده نیاز دارند.
بررسی‌های scope سیاست‌های `agent:scope:*` هستند، نه مجوزهای RBAC — هویت ماشین هرگز اعطای `module.action` را resolve نمی‌کند.

| متد | مسیر | احراز هویت |
|---|---|---|
| POST | `/api/v1/agents/auth/token` با client_id و client_secret و JWT | Anonymous |
| GET | `/api/agent/prolog?deviceId` (دست‌تکان پیش‌از‌احراز) | Anonymous — با `[AllowAnonymous]` صریح، ادامه را ببینید |
| POST | `/api/agent/inventory`، ‏`/discovery`، ‏`/netinventory`، ‏`/esx`، ‏`/wakeonlan` | `agent:scope:inventory:write` |
| POST | `/api/agent/deploy/result` | agent scope |
| GET | `/api/agent/deploy/jobs?deviceId` | agent scope |
| GET | `/api/agent/commands/pending` | agent scope (فقط دستورهای خودش) |
| POST | `/api/agent/commands/{id}/acknowledge` و `/{id}/result` | agent scope (فقط دستورهای خودش) |
| GET | `/api/agent/commands/{id}/result` | agent scope |
| ادمین | `/api/v1/agents*`، ‏`/api/v1/agents/{id}/credentials`، ‏`/audit`، ‏`/commands` | JWT کاربر و اعطاهای ادمین |

`prolog` تنها اندپوینت ناشناس غیرلاگین در سامانه است و ناشناس‌بودنش *صریح* است (`[AllowAnonymous]` در سطح متد که سیاست scope کنترلر را override می‌کند)، چون ایجنت‌ها پیش از داشتن هر توکنی آن را صدا می‌زنند. فقط شناسه دستگاه را می‌گیرد و پارامترهای غیرحساس سرور را برمی‌گرداند — بدون دسترسی به داده، بدون تغییر وضعیت. دو نکته صداقت، هر دو با `TODO (Task 5.x)` در کد علامت خورده‌اند: `prolog` فعلاً همیشه `known: false` جواب می‌دهد (نگهدارنده، نه lookup)، و `deploy/jobs` همیشه `[]` جواب می‌دهد. اگر هر کدام فراتر از این رشد کردند، اول ناشناس‌بودن را بازبینی کنید.

ماژول Inventory هیچ کنترلر Api ماژولی ندارد: دریافت این‌جاست.

## ۱۰. هاب‌های SignalR (پوش سرور، نه RPC)

| هاب | هاست / مسیر | جهت | هدف |
|---|---|---|---|
| `AgentStatusHub` | هاست Api در `/hubs/agent-status` | به مرورگر | حضور زنده ایجنت برای داشبورد ناوگان |
| `TicketHub` | هاست Api در `/hubs/tickets` | به مرورگر | به‌روزرسانی زنده تیکت (فرانت‌اند: `ticket-hub.service`) |
| `AgentControlHub` | هاست AgentApi در `/hubs/agent-control` | به ایجنت | پوش اعزام دستور به ماشین‌های ناوگان |

مذاکره هاب مثل هر اندپوینت دیگر JWT از نوع Bearer است (توکن در query-string برای ارتقای WebSocket). تایم‌اوت‌های لبه ۷ روز است — اتصال‌های هاب ساعت‌ها زنده می‌مانند؛ پیش‌فرض ۶۰ ثانیه آن‌ها را خاموش می‌کشت.

## ۱۱. جریان‌های احراز هویت

**کاربر (تعاملی).** ثبت‌نام و ورود `{ accessToken, accessTokenExpiresAt, user }` را برمی‌گردانند و refresh token را به‌صورت کوکی HttpOnly با SameSite=Strict محدود به `/api/v1/identity/auth` می‌نشانند. توکن‌های دسترسی ۱۵ دقیقه عمر می‌کنند و `sub`، ‏`active_entity_id`، ‏`name`، ‏`email` و `token_version` را حمل می‌کنند. Refresh توکن مات را سمت سرور می‌چرخاند (استفاده مجدد از توکن چرخیده، کل خانواده را باطل می‌کند — واکنش به سرقت است، نه خطایی برای retry). خروج خانواده را باطل و کوکی را پاک می‌کند.

```bash
# ثبت‌نام و ورود (فرانت‌اند بعد از اصلاح URL در وظیفه ۸.۴ دقیقاً همین را انجام می‌دهد)
curl -s -X POST https://$DOMAIN/api/v1/identity/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"username":"alice","email":"alice@example.com","password":"correct-horse-battery-staple-1","displayName":"Alice"}'
curl -s -c cookies.txt -X POST https://$DOMAIN/api/v1/identity/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"usernameOrEmail":"alice","password":"correct-horse-battery-staple-1"}'
# فراخوانی واقعی با توکن دسترسی پاسخ لاگین:
curl -s https://$DOMAIN/api/v1/tickets/count/open -H "Authorization: Bearer $ACCESS"
# تازه‌سازی (کوکی کار را می‌کند):
curl -s -b cookies.txt -c cookies.txt -X POST https://$DOMAIN/api/v1/identity/auth/refresh
```

تورهای ایمنی حساب، همه سمت سرور: ۵ خطا یعنی قفل ۱۵ دقیقه‌ای (شمارنده با پایان پنجره صفر می‌شود)؛ حساب‌های غیرفعال/حذف‌شده هم در login و هم در refresh بسته fail می‌کنند (جایگزین چرخیده تحویل داده نمی‌شود، باطل می‌شود)؛ بالا بردن `TokenVersion` هر توکن دسترسی معلق را در درخواست بعدی نامعتبر می‌کند؛ حساب‌های ساخته ادمین هش گذرواژه ندارند و تا وقتی گذرواژه ست نشود نمی‌توانند وارد شوند.

**ایجنت (ماشین).** از نوع `client_credentials` بدون refresh token — ایجنت‌ها پیش از انقضا دوباره احراز هویت می‌کنند (۶۰ ثانیه زودتر، با double-checked locking).

```bash
curl -s -X POST https://$DOMAIN/api/v1/agents/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"da_…","clientSecret":"sk_…","scope":"inventory:write"}'
# → { accessToken, tokenType: "Bearer", expiresIn, scope }
```

‏JWT صادره `sub` (شناسه ایجنت)، ‏`agent_type`، ‏`scope` (اشتراک درخواستی با اعتبارنامه با مجاز ایجنت؛ تطابق‌های جزئی رد می‌شوند، نه تنزل)، ‏`is_agent=true` و `active_entity_id` را حمل می‌کند. کلاینت ناشناس و سکرت اشتباه ۴۰۳های بایت‌به‌بایت یکسان برمی‌گردانند (اوراکل شمارش نیست). ابطال یعنی غیرفعال‌کردن اعتبارنامه یا ایجنت؛ اعتبارنامه عام *پیش* از اشتراک resolve می‌شود، پس `*` یعنی «هر چه این ایجنت مجاز است».

## ۱۲. خطاها — یک شکل همه‌جا

هر سرویس ماژول `Result<T>` برمی‌گرداند؛ هر کنترلر آن را از تنها `ErrorProblemDetailsExtensions.ToProblemResult` مشترک عبور می‌دهد (ADR-003). شکل سیم RFC 7807 است:

```json
{
  "title": "Ticket not found.",
  "status": 404,
  "type": "https://daraban.local/errors/notfound",
  "errorCode": "TICKET.NOT_FOUND",
  "traceId": "00-…"
}
```

| نوع `ErrorType` | ‏HTTP | معنا |
|---|---|---|
| `Validation` | ۴۰۰ | ورودی بدشکل (FluentValidation و گاردهای سرویس) |
| `NotFound` | ۴۰۴ | شناسه ناشناس (فراتر از همین، چیزی لو نمی‌دهد) |
| `Conflict` | ۴۰۹ | مقدار یکتای تکراری (نام، تگ، شماره سفارش، نصب) |
| `Forbidden` | ۴۰۳ | احراز هویت‌شده ولی غیرمجاز (بین‌tenant، قفل‌شده، اعتبار باطل‌شده) |
| `BusinessRule` | ۴۲۲ | درخواست خوش‌فرم که ماشین‌وضعیت رد می‌کند |

قرارداد `errorCode` به شکل `MODULE.SCREAMING_SNAKE` است (`IDENTITY.*`، ‏`ASSETS.*`، ‏`TICKET.*`، ‏`TICKET_TASK.*`، ‏`TICKET_TEMPLATE.*`، ‏`KNOWLEDGE.*`، ‏`SOFTWARE.*`، ‏`LICENSE.*`، ‏`INSTALLATION.*`، ‏`BUDGET.*`، ‏`CONTRACT.*`، ‏`SUPPLIER.*`، ‏`PURCHASE.*`، ‏`INFOCOM.*`، ‏`AGENTS.*`، ‏`INVENTORY.*`).
دیسکاوری استثنای مؤید قاعده است: سرویس‌هایش `InvalidOperationException` پرتاب می‌کنند که هندلر سراسری آن را ۵۰۰ *بدون* `errorCode` رندر می‌کند — خطاهای ساخت‌یافته آن‌جا پیگیری بعدی است و هر کد کلاینت باید ۵۰۰ از `/api/v1/discovery/*` را مات بداند.

## ۱۳. محدودیت‌های نرخ (دو لایه، بودجه‌های یکسان)

| لایه | سیاست | بودجه | اعمال روی |
|---|---|---|---|
| ‏ASP.NET در `Program.cs` | `auth` | ۱۰ درخواست/دقیقه، رد مازاد | `identity/auth/*` با `[EnableRateLimiting]` |
| ASP.NET | `discovery-scan` | ۵ در دقیقه و صف ۲ | شروع اسکن |
| ‏nginx در prod | زون `auth` | ۱۰ درخواست/دقیقه/IP با burst ‏۵ | `/api/v1/identity/auth/` |
| ‏nginx در prod | زون `agent_auth` | ۶۰ درخواست/دقیقه/IP با burst ‏۱۰ | `/api/v1/agents/auth` |
| ‏nginx در prod | زون `api` | ۱۰۰ درخواست/ثانیه/IP با burst ‏۲۰۰ | بقیه |

سرریز در هر دو لایه **۴۲۹** جواب می‌دهد. لبه و اپ بودجه لاگین یکسان را اعمال می‌کنند تا هیچ‌کدام نقطه دورزدن تکی نباشد؛ فیکسچر یکپارچه محدودکننده داخل‌اپ را خنثی می‌کند (IP لوپ‌بک مشترک) تا تست‌های موازی روی ۴۲۹ها flake نشوند.

## ۱۴. صفحه‌بندی، فیلتر و شکل پاکت‌ها

اندپوینت‌های فهرست `page` (بزرگ‌ترمساوی ۱، پیش‌فرض ۱) و `pageSize` (پیش‌فرض ۲۰، سقف سخت ۲۰۰ — مقدارهای بزرگ‌تر clamp می‌شوند، نه رد) می‌گیرند. قبلاً `page=0` بدون clamp تولید `Skip(-pageSize)` و خطای Postgres می‌کرد؛ اکنون هر سرویس نرمال می‌کند. پاسخ‌ها یک پاکت مشترک دارند:

```json
{ "items": [ … ], "totalCount": 137, "page": 2, "pageSize": 20 }
```

فیلترها پارام کوئری هر منبع‌اند (`status`، ‏`categoryId`، ‏`isFaq`، ‏`supplierId` و غیره — جدول‌های بالا را ببینید)؛ جست‌وجوی آزاد تطابق زیررشته‌ای است به‌جز جست‌وجوی KB که تمام‌متن رتبه‌بندی‌شده با relevance است (`ts_rank`، ‏`websearch_to_tsquery`، ایندکس GIN). ترتیب‌ها ثابت هر اندپوینت‌اند (جدیدترین‌اول برای فیدها، ترتیب نام برای کاتالوگ‌ها، ترتیب sort-order برای درخت‌ها).

## ۱۵. خروجی Swagger / OpenAPI

هر دو هاست Swagger UI را فقط در Development سرو می‌کنند. برای خروجی قرارداد:

```bash
# در برابر استک لوکال (کامپوز dev کافی است -- خواندن Swagger نیاز به احراز هویت ندارد):
curl -s http://localhost:8080/swagger/v1/swagger.json -o openapi.host-api.json
curl -s http://localhost:8081/swagger/v1/swagger.json -o openapi.host-agentapi.json
```

کلاینت Angular از این فایل‌ها تولید می‌شود — وقتی بک‌اند عوض شد، بازتولید کنید، دستی ویرایش نکنید.

## ۱۶. رویه تازه‌سازی این سند

این فایل نوشته نشده، تولید شده است: `docs/gen-endpoints.ps1` همه `*Controller.cs`ها را می‌گردد، هر اکشن را با بلوک اتریبیوتش (`[HttpX]`، ‏`[RequirePermission]`، ‏`[AllowAnonymous]`، سیاست محدودیت نرخ) جفت می‌کند و `module | METHOD | path | auth` را بیرون می‌دهد. بعد از هر تغییر کنترلر، بازتولید و diff کنید: سطر جدید با auth خالی یعنی `[Authorize]` گم‌شده تا خلافش ثابت شود. سیاست‌های scope در AgentApi با `agent:scope:*` و روت‌های دارای توکن `[controller]` در Financial به حاشیه‌نویسی دستی §۴ و §۹ نیاز دارند — ژنراتور آن‌ها را خام گزارش می‌کند.
