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
