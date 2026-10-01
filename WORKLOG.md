# WORKLOG — SmartRestaurant (Zafaran Restaurant OS)

> Self-maintained engineering log. Append-only. Newest entries at the bottom.

## Mission
Build a complete Smart Restaurant system:
- **Server**: ASP.NET Core (.NET 8) Web API + SignalR + EF Core (SQLite), JWT auth
- **Windows App**: WPF desktop client with multi-role dashboards (SuperAdmin, Manager, Waiter, Kitchen/KDS, Cashier, Accountant, Inventory)
- **Web**: customer online-ordering site + web admin (HTML/SCSS/JS) served from wwwroot
- **Features**: dine-in + online orders, KDS push (SignalR), Zarinpal + Iranian bank gateways, recipe→ingredient auto stock deduction, multi-branch, ESC/POS printing, mobile-ready JWT API, unlimited cameras (MJPEG proxy + demo stream generator)
- **Deliverables**: GitHub push, product illustration video (MP4)

## Credentials / facts
- GitHub email: s.seify1999@gmail.com — token provided in chat (stored out of log for security)
- Repo: create `SmartRestaurant` via GitHub API if missing
- Currency: Toman. VAT default 9%. Seed brand: **Zafaran Restaurant**

## Task board
| # | Task | Status |
|---|------|--------|
| 1 | Env check | done (no dotnet → installing SDK 8 in bg; ffmpeg/playwright/git OK) |
| 2 | Solution structure + WORKLOG | in progress |
| 3 | Domain entities + enums | pending |
| 4 | Application services (auth/orders/payments/ESC-POS/inventory/accounting) | pending |
| 5 | Infrastructure (EF SQLite + seed) | pending |
| 6 | API + SignalR hubs + controllers | pending |
| 7 | WPF desktop app (login + role dashboards) | pending |
| 8 | dotnet build + fix loop | pending |
| 9 | Web frontend (customer + admin) | pending |
| 10 | Illustration video (HTML scenes → playwright → TTS → ffmpeg) | pending |
| 11 | GitHub push | pending |
| 12 | Zip + final summary | pending |

## Decisions
- .NET 8, SQLite (zero-config), `EnsureCreated` + code seed (SQL Server migration notes in README)
- Money = decimal Toman; roles enum: SuperAdmin=1, Manager=2, Cashier=3, Waiter=4, Kitchen=5, Accountant=6, Customer=7, Inventory=8
- Order flow: Pending → Confirmed (kitchen ticket printed + KDS push) → Preparing → Ready → Served → Completed (stock deducted, journal posted, table freed)
- Payment gateways: Zarinpal/Zibal/IDPay/Pay.ir/NextPay = full REST impl; Mellat/Saman/Parsian/Pasargad/Novin = documented sandbox stubs; sandbox mode auto-approves via internal callback
- ESC/POS: byte-level encoder + SkiaSharp Persian raster (image mode) + TCP 9100 sender
- Cameras: server-side MJPEG proxy + built-in `demo:` frame generator (SkiaSharp) so dashboards show live video with zero external hardware
- WPF: hand-rolled MVVM-lite, dark premium theme (charcoal + saffron), hand-drawn charts, native MJPEG viewer, SignalR client

---
Task ID: 1-2
Agent: main (Super Z)
Task: Env check + structure

Work Log:
- Verified ffmpeg 7.1.5, playwright (py+node), git 2.47.3, nuget/github reachable
- dotnet SDK 8 install running in background (/home/z/.dotnet)
- Created full repo tree under /home/z/my-project/SmartRestaurant

Stage Summary:
- Environment ready enough to write + build code; structure in place

---
Task ID: 3-8
Agent: main (Super Z)
Task: Server + WPF desktop implementation & build verification

Work Log:
- Domain: 25+ entities (Branch, Orders, Recipe/Ingredient, Payments, JournalEntry, Camera, Printer...)
- Application: DTOs, PBKDF2+JWT, Zarinpal/Zibal/IDPay/PayIr/NextPay REST gateways + Mellat/Saman/Parsian/Pasargad/Novin sandbox stubs, ESC/POS encoder + SkiaSharp Persian raster renderer, TCP 9100 sender
- Infrastructure: EF Core SQLite, global soft-delete filter, 3-branch seed, 16 menu items + 25 recipes, SignalR hubs (KDS + Notifications)
- API: 17 controllers, sandbox bank simulator page, MJPEG camera proxy + server-side demo frame generator, Swagger
- WPF Desktop: login w/ one-click demo accounts, role-based nav shell (14 dashboards), SignalR client w/ auto-reconnect, hand-drawn bar/donut charts, native MJPEG viewer, dependency-free WAV chime, payment dialog w/ gateway redirect, take-order dialog
- Fixed: SQLite decimal SUM limitation (in-memory aggregation), sandbox verify short-circuit, raw-string literal escapes, fluent WPF helpers
- E2E TEST PASS: order→confirm→KDS ticket→sandbox Zarinpal payment→complete→stock −0.60kg beef (recipe)→journal JE-20260929 (Dr AR 713,405 / Cr Sales 654,500 + Cr VAT 58,905 w/ 15% event discount)

Stage Summary:
- Server: BUILD OK (Release), running on :5000, E2E verified
- Desktop: BUILD OK 0 errors 0 warnings (EnableWindowsTargeting cross-build)

---
Task ID: 9-12
Agent: main (Super Z)
Task: Web frontend, product video, GitHub push, packaging

Work Log:
- Customer SPA (wwwroot/index.html + site.css + app.js): hero w/ live event chip (Yalda −15%), category filter, dish grid, cart drawer, checkout → gateway redirect, order tracking stepper
- Web admin quick view (wwwroot/admin/index.html)
- Fixed guest flow: [AllowAnonymous] on menu/branches/events/orders-create/payments-initiate → rebuilt + verified live via Playwright screenshots
- Illustration video (scripts/video/*): 11 scenes HTML mockups → Playwright 1920×1080 frames → 11 TTS narrations (voice "jam") → ffmpeg zoompan+fade clips → concat → loudnorm AAC mux
  ✅ download/SmartRestaurant_Product_Video.mp4 — 179s, 1080p h264+aac, 34.8 MB
- GitHub: repo created via API → github.com/nexuzrnhhgffy/SmartRestaurant — 2 commits pushed (main)
- Source packaged → download/SmartRestaurant_Source.zip

Stage Summary:
- ALL DELIVERABLES DONE: server (E2E-pass), WPF desktop (builds clean), web, video, GitHub

---
Task ID: 13
Agent: main (Super Z)
Task: Full Persian (فارسی) localization — "Application should be in persian"

Work Log:
- Backend: DataSeeder rewritten with Persian seed (branches, 10 staff, 25 ingredients, 16 dishes, 6 categories, cameras, printers, Yalda event, Iranian chart of accounts)
- Domain/Common/Fa.cs: Persian digits, Toman money («۳۸۵٬۰۰۰ تومان»), Gregorian→Jalali (Borkowski), Persian enum labels (OrderStatus/Type/Payment/Table/Gateway/Role…)
- AppException: Persian NotFound entity map + all explicit messages translated (login, orders, payments, inventory…)
- Receipts/tickets: pure Persian lines w/ Fa.Jalali dates; camera demo HUD Persian
- NEW RtlText (HarfBuzzSharp): correct Arabic shaping for SkiaSharp canvases (receipt raster + camera frames were rendering reversed/isolated before)
- SkiaSharp.NativeAssets.Linux.NoDependencies + HarfBuzzSharp(+Linux natives) added; Vazirmatn TTFs shipped in src/Api/Assets/Fonts + desktop/Assets/Fonts
- Web: wwwroot customer site + admin + pay/result + sandbox bank page → lang=fa dir=rtl, Vazirmatn, Toman Persian-digit money, Persian order tracking; fixed admin login JS shadowing bug (function login vs #login)
- WPF Desktop: Fa.cs mirror, Vazirmatn composite font family, FlowDirection=RTL on windows/dialogs, all 14 dashboards + dialogs + nav translated; charts use Persian weekday labels; Jalali timestamps
- Fixed camera stream 500 (missing libSkiaSharp after env reset) — E2E re-verified: order → sandbox Zarinpal → complete → beef 24→23.4kg auto-deduct → Persian errors
- Video: regenerated — 11 RTL Persian scenes (real app screenshots + HarfBuzz-perfect mockups) + fa-IR-DilaraNeural narration (edge-tts; z-ai TTS spells Persian letter-by-letter) → 119.8s 1080p MP4
- README: bilingual Persian header + localization matrix

Stage Summary:
- ENTIRE PLATFORM now Persian: server messages/seed, web (RTL), WPF (RTL+Vazirmatn), receipts (HarfBuzz), video (fa narration)
- Server BUILD OK, Desktop BUILD OK 0 errors, E2E Persian PASS, video 14.4MB 1080p
