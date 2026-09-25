# Aurora Smart Restaurant 🍽

A complete, production-shaped **Smart Restaurant Platform** — dine-in POS, online ordering,
kitchen display, events & catering branch, inventory, accounting, staff management and
role-based dashboards. Built with **ASP.NET Core 8 (.NET) + EF Core + SQLite** on the backend
and a hand-crafted **HTML / SCSS / JS** frontend (zero UI frameworks, fully custom design system).

![stack](https://img.shields.io/badge/.NET-8.0-512BD4) ![db](https://img.shields.io/badge/EF_Core-SQLite-00695C) ![fe](https://img.shields.io/badge/frontend-HTML%20%2F%20SCSS%20%2F%20JS-E8A33D)

---

## ✨ Feature Matrix

| Module | Highlights |
|---|---|
| 🛒 **Online Restaurant** | Public site, menu with modifiers, cart, coupons, delivery & pickup, live tax/service/delivery fee engine, review submission, table reservations, event quote requests |
| 🧾 **Dine-in POS** | Floor map per section, table statuses (available/reserved/occupied/dirty/OOS), walk-in & QR-ready flows, open bills, add/remove items, tips, split-aware payments (cash change calculator) |
| 👨‍🍳 **Kitchen (KDS)** | Standalone full-screen display, 3-column ticket flow (New → In Progress → Ready), per-line bumping, late-ticket alerts (>12m/>20m) with audio chime, station filters (Grill/Fry/Cold/Pastry/Bar/HotLine) |
| 🎉 **Events Branch** | Packages (wedding/corporate/birthday...), inquiry → quote → deposit pipeline, quote auto-pricing per guest, operational checklists with staff assignment |
| 📦 **Inventory** | Ingredients, suppliers, purchase orders (draft→ordered→received), stock movements ledger (purchase/usage/waste/adjustment), low-stock alerts pushed to notifications |
| 💰 **Accounting** | Expenses by category, payroll generation with overtime & bonuses, paid tracking, full **P&L report** (revenue, food cost %, expenses, payroll, net margin) with charts |
| 👔 **Staff & HR** | Role accounts, PIN fast-login (POS stations), shifts scheduling, attendance check-in/out with hours, payroll per employee |
| 🤝 **Customers & Loyalty** | CRM with auto loyalty points per $, Bronze→Platinum tiers, spend history |
| 📊 **Dashboards per role** | KPI cards, revenue trend, hourly sales, top sellers, order-type & payment mix — every role sees only its own modules |
| 🔔 **Notifications** | In-app center with unread badges — low stock, new orders, reservations, event inquiries, pending reviews |
| 🏷 **Marketing** | Percent/fixed coupons with min-order, usage limits & expiry; moderation + public replies for reviews |
| ⚙ **Settings** | Restaurant identity, tax/service/delivery rates, loyalty rate, ordering & reservation toggles (SuperAdmin) |

### Roles
`SuperAdmin` · `Manager` · `Cashier` · `Waiter` · `Chef` · `Accountant` · `Customer`
Each role gets a different sidebar, landing module and API permissions (JWT role policies).

---

## 🚀 Quick Start

```bash
# 1) requirements: .NET 8 SDK
cd src/SmartRestaurant.Api
dotnet run --urls http://localhost:5080

# 2) open
#    Public site  → http://localhost:5080/
#    Admin panel  → http://localhost:5080/admin.html
#    Kitchen KDS  → http://localhost:5080/kitchen.html
#    Swagger API  → http://localhost:5080/swagger
```

The SQLite database (`App_Data/smartrestaurant.db`) is **created and seeded automatically**
on first start: 9 users, 13 tables, 15 dishes with modifiers, 30 days of historical orders
for analytics, live kitchen tickets, events, expenses, payroll, reviews and more.

### Demo Accounts

| Role | Email | Password |
|---|---|---|
| SuperAdmin | superadmin@restaurant.com | Super@123 |
| Manager | manager@restaurant.com | Manager@123 |
| Cashier | cashier@restaurant.com | Cashier@123 |
| Waiter | waiter@restaurant.com | Waiter@123 |
| Chef | chef@restaurant.com | Chef@123 |
| Accountant | accountant@restaurant.com | Account@123 |
| Customer | guest@restaurant.com | Guest@123 |

POS PIN fast-login: `1111` SuperAdmin · `2222` Manager · `3333` Cashier · `4444` Waiter · `5555` Chef · `6666` Accountant

Try coupon **WELCOME15** on an online order 🎟

---

## 🏗 Architecture (improved Clean-ish pattern)

```
SmartRestaurant/
├── src/
│   ├── SmartRestaurant.Domain/            # Entities, enums, domain rules (no dependencies)
│   │   └── Entities/{Identity,Menu,Orders,Tables,Reservations,Inventory,Events,Accounting,Customers,Notifications,Settings}
│   ├── SmartRestaurant.Application/       # DTOs + service interfaces (contracts)
│   │   ├── Dtos/                          # Immutable record DTOs for every module
│   │   └── Interfaces/IServices.cs        # 16 service contracts
│   ├── SmartRestaurant.Infrastructure/    # EF Core, services, auth, seeding
│   │   ├── Data/AppDbContext.cs           # Fluent config, global soft-delete query filters, decimal precision
│   │   ├── Data/DbSeeder.cs               # Rich demo data (30-day analytics history)
│   │   ├── Data/Migrations/               # EF Core migration
│   │   └── Services/                      # 16 service implementations (JWT auth, pricing engine, KDS, P&L...)
│   └── SmartRestaurant.Api/               # HTTP layer
│       ├── Controllers/ApiControllers.cs  # 15 controllers, role-based authorization
│       └── Program.cs                     # DI, JWT bearer, CORS, Swagger, static frontend hosting
├── frontend/                              # Served by Kestrel (no Node needed at runtime)
│   ├── index.html                         # Public restaurant site
│   ├── admin.html                         # Role dashboard SPA shell
│   ├── kitchen.html                       # Kitchen Display System
│   ├── scss/                              # Design system (variables/mixins) + 3 themes
│   ├── css/                               # Compiled CSS
│   ├── js/                                # core.js (API client) + app.js + admin.js/admin2.js + charts.js + kitchen.js
│   └── assets/img/                        # Branded SVG artwork
└── docs/
```

**Patterns used:** Layered Clean Architecture with dependency inversion · Repository-via-DbContext ·
DTO boundary (records) · Service layer per bounded module · Global soft-delete query filter ·
JWT claims-based RBAC · Option-pattern configuration · Auto-migration + idempotent seeding ·
Polling-based realtime KDS (5–8 s) with audio feedback.

### Security notes
- BCrypt password hashing, JWT bearer (12 h), role policies on every mutating endpoint
- Anonymous endpoints limited to: public menu/settings/packages/reviews, coupon validation,
  reservation & event inquiry forms, and guest order creation
- Soft delete filter applied globally at model level

---

## 🔧 Development

```bash
# rebuild SCSS after editing scss/
sass frontend/scss/public.scss frontend/css/public.css
sass frontend/scss/admin.scss  frontend/css/admin.css
sass frontend/scss/kitchen.scss frontend/css/kitchen.css

# reset demo data
rm src/SmartRestaurant.Api/App_Data/smartrestaurant.db*

# run the API test suite (while the app is running)
bash scripts/smoke_test.sh   # 41 checks: auth, RBAC, orders, KDS, payments, coupons, modules
```

### API surface (v1)
`/api/auth` · `/api/users` · `/api/menu` · `/api/tables` · `/api/reservations` · `/api/orders` ·
`/api/payments` · `/api/kitchen` · `/api/inventory` · `/api/events` · `/api/accounting` ·
`/api/staff` · `/api/dashboard` · `/api/customers` · `/api/reviews` · `/api/coupons` ·
`/api/notifications` · `/api/settings` — full docs at `/swagger`.

---

## 🗺 Roadmap ideas
- SignalR for push-based KDS & notifications
- Stripe/PayPal gateway integration for online payments
- Recipe–ingredient mapping to auto-deduct stock on order completion
- Multi-branch support, printer integration (ESC/POS), mobile app over the same JWT API
