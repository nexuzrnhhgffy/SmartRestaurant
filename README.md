# 🫖 Zafaran — Smart Restaurant Operating System

A complete, production-grade restaurant platform: **ASP.NET Core 8 server + WPF Windows desktop app + customer web ordering + mobile-ready JWT API** — with push-based Kitchen Display (SignalR), **Zarinpal & Iranian bank gateways**, **recipe-driven inventory auto-deduction**, multi-branch, **ESC/POS printing**, double-entry accounting, and **unlimited live camera streams**.

```
┌────────────────────────┐      ┌──────────────┐  ┌───────────────┐  ┌──────────────┐
│  SmartRestaurant.Server │ ⇄──  │ Zafaran      │  │ Customer Web  │  │ Mobile apps  │
│  ASP.NET Core 8 API     │      │ Desktop(WPF) │  │ ordering site │  │ (same JWT)   │
│  SignalR · EF · SQLite  │      │ 14 dashboards│  │ wwwroot       │  │              │
└───────────┬────────────┘      └──────────────┘  └───────────────┘  └──────────────┘
            │
   ┌────────┴─────────┐   ┌────────────────┐   ┌──────────────┐
   │ ESC/POS printers │   │ IP cameras     │   │ Zarinpal /   │
   │ (TCP 9100)       │   │ MJPEG proxy    │   │ Zibal / IDPay│
   └──────────────────┘   └────────────────┘   │ /Pay.ir/NextPay/ Mellat·Saman·Parsian │
                                               └──────────────┘
```

## ✨ Feature matrix

| Area | Details |
|---|---|
| **Orders** | Dine-in / Takeaway / Delivery, order state machine (Pending→Confirmed→Preparing→Ready→Served→Completed→Cancelled), per-item KDS stations, running-event auto discount, 9% VAT |
| **KDS (real-time)** | SignalR push tickets per branch group, per-station filters (Grill/Hot/Cold/Dessert/Bar), bump bars (Queued→Cooking→Ready→Delivered), late-ticket timers + audio chime |
| **Payments** | Full REST impls: **Zarinpal v4, Zibal, IDPay, Pay.ir, NextPay** · documented sandbox stubs: **Mellat, Saman, Parsian, Pasargad, Novin** (SOAP notes inline) · internal sandbox bank simulator page · cash/POS shortcuts · gateway config CRUD |
| **Inventory** | Recipe (BOM) per menu item → **auto stock deduction on order completion** → StockMovement ledger → low-stock notifications (Manager + Inventory) |
| **Multi-branch** | Branch-scoped entities, SuperAdmin sees all / staff see own, branch comparison report, per-branch tables/printers/cameras |
| **Accounting** | Iranian chart of accounts, **auto double-entry journal per sale** (Dr Cash/AR · Cr Revenue · Cr VAT), expenses w/ auto journal, trial balance, P&L |
| **Printing** | ESC/POS byte-level encoder (init/align/bold/size/codepage/cut), **Persian-safe raster mode** (SkiaSharp → 1-bpp GS v 0 bitmap), TCP 9100 sender, PrintJob audit trail, kitchen tickets + guest receipts |
| **Cameras** | Server-side MJPEG proxy + **built-in demo stream generator** (SkiaSharp frames — zero hardware needed), WPF native MJPEG viewer (no VLC/WebView2), RTSP via MediaMTX documented |
| **Auth** | JWT (role claim) + refresh tokens, PBKDF2-SHA256 100k iterations, 8 roles, audit log |
| **Web** | Customer SPA (menu, cart, checkout → gateway redirect, live order tracking) + quick web admin |
| **API** | Swagger UI at `/swagger`, versioned `/api/v1`, mobile-ready, SignalR hubs `/hubs/kds` + `/hubs/notifications` |

## 🚀 Quick start (server)

```bash
cd src/Api
dotnet run -c Release          # http://0.0.0.0:5000 — DB auto-creates + seeds
```

- Swagger: `http://localhost:5000/swagger`
- Customer site: `http://localhost:5000/`
- Web admin: `http://localhost:5000/admin/`

> SQL Server instead of SQLite: change `ConnectionStrings:Default` and `UseSqlite` → `UseSqlServer` in `Infrastructure/ServiceRegistration.cs`, then add migrations.

## 🪟 Windows app

Open `desktop/SmartRestaurant.Desktop.csproj` in Visual Studio 2022 (or `dotnet run` on Windows). Point the login screen at the server URL (default `http://localhost:5000`), pick a demo account, done. Settings persist in `%AppData%/Zafaran/desktop-settings.json`.

## 👤 Demo accounts

| Role | Username | Password |
|---|---|---|
| SuperAdmin | `superadmin` | `Admin@123` |
| Manager | `manager` / `manager2` | `Manager@123` |
| Cashier | `cashier` | `Cashier@123` |
| Waiter | `waiter` / `waiter2` | `Waiter@123` |
| Kitchen (KDS) | `kitchen` | `Kitchen@123` |
| Accountant | `accountant` | `Acc@123` |
| Inventory | `inventory` | `Stock@123` |
| Customer | `customer` | `Customer@123` |

## 🔌 API quick tour

```bash
# login
curl -X POST localhost:5000/api/v1/auth/login -H 'Content-Type: application/json' \
     -d '{"userName":"cashier","password":"Cashier@123"}'

# create a dine-in order (Authorization: Bearer <token>)
curl -X POST localhost:5000/api/v1/orders -H 'Content-Type: application/json' -H 'Authorization: Bearer '$T \
     -d '{"type":1,"items":[{"menuItemId":"<guid>","quantity":2,"notes":"extra saffron"}]}'

# confirm → kitchen ticket prints + KDS push
curl -X PUT localhost:5000/api/v1/orders/<id>/status -d '{"status":2}' -H 'Authorization: Bearer '$T

# take payment (sandbox Zarinpal) → visit redirectUrl → auto-verify → order Paid
curl -X POST localhost:5000/api/v1/payments/initiate -d '{"orderId":"<id>","gateway":10}' -H 'Authorization: Bearer '$T

# complete → stock auto-deducted + journal posted + receipt queued
curl -X PUT localhost:5000/api/v1/orders/<id>/status -d '{"status":6}' -H 'Authorization: Bearer '$T
```

Mobile apps: same endpoints + `POST /auth/refresh`, `GET /menu/items`, `POST /orders`, `GET /orders/{id}` — nothing else needed.

## 📹 Cameras

Demo cameras (`demo:grill` …) render animated frames **server-side** — the whole feature works with zero hardware. Real LAN MJPEG cameras: just set the URL (server proxies them, fixing CORS/mixed-content). RTSP cameras: run [MediaMTX](https://github.com/bluenviron/mediamtin) or go2rtc in front and point the camera to its MJPEG/HLS output.

## 🖨 Printers

Seed defaults point to `127.0.0.1:9100/9101` — change Host/Port in the desktop Settings/Printers. `UseRasterMode=true` renders receipts to a 1-bpp image (guaranteed Persian output on any ESC/POS printer). Printing failures never block the order flow — jobs are logged in `PrintJobs`.

## 🧪 E2E verified

`scripts/e2e_test.py` walks the full pipeline: order → confirm → KDS ticket → sandbox Zarinpal → capture → complete → **stock −0.60 kg beef** → balanced journal → dashboard totals. ✅

## 📂 Structure

```
SmartRestaurant/
├── src/
│   ├── Domain/          # entities + enums (EF-free)
│   ├── Application/     # DTOs, services contracts, JWT, gateways, ESC/POS
│   ├── Infrastructure/  # EF Core DbContext, seed, business services, SignalR hubs
│   └── Api/             # controllers, middleware, camera streams, wwwroot (web)
├── desktop/             # WPF app: login, role shell, 14 dashboards, SignalR client
└── WORKLOG.md           # engineering log
```
