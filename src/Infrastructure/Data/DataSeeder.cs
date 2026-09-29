using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Security;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Infrastructure.Data;

/// <summary>Ensures the database exists and seeds a fully demo-ready restaurant.</summary>
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return; // already seeded

        // ── Settings ──────────────────────────────────────────────
        db.Settings.AddRange(
            new AppSetting { Key = "Restaurant:Name", Value = "Zafaran Restaurant", Description = "Brand shown on receipts & website" },
            new AppSetting { Key = "Restaurant:TaxRatePercent", Value = "9", Description = "VAT percentage" },
            new AppSetting { Key = "Restaurant:Currency", Value = "Toman", Description = "Display currency" },
            new AppSetting { Key = "Receipt:Footer", Value = "نوش جان — با تشکر از انتخاب شما", Description = "Printed at the receipt bottom" },
            new AppSetting { Key = "Kds:LateMinutes", Value = "15", Description = "Ticket turns red after this many minutes" });

        // ── Branches ──────────────────────────────────────────────
        var bTehran = new Branch { Name = "Zafaran Tehran — Flagship", City = "Tehran", Address = "Valiasr St., No. 124", Phone = "021-22334455" };
        var bKaraj = new Branch { Name = "Zafaran Karaj", City = "Karaj", Address = "Chaloos Rd., Mehrshahr", Phone = "026-33445566" };
        var bOnline = new Branch { Name = "Cloud Kitchen — Delivery Hub", City = "Tehran", Address = "Saadat Abad", Phone = "021-88776655" };
        db.Branches.AddRange(bTehran, bKaraj, bOnline);

        // ── Users (all passwords documented in README) ─────────────
        var hasher = new Pbkdf2PasswordHasher();
        AppUser U(string full, string userName, string pwd, UserRole role, Guid? branch, string email) =>
            new() { FullName = full, UserName = userName, Email = email, Phone = "0912" + Random.Shared.Next(1000000, 9999999), PasswordHash = hasher.Hash(pwd), Role = role, BranchId = branch };
        db.Users.AddRange(
            U("Sara Seify", "superadmin", "Admin@123", UserRole.SuperAdmin, null, "s.seify1999@gmail.com"),
            U("Nima Hosseini", "manager", "Manager@123", UserRole.Manager, bTehran.Id, "manager@zafaran.ir"),
            U("Leila Karimi", "manager2", "Manager@123", UserRole.Manager, bKaraj.Id, "manager2@zafaran.ir"),
            U("Omid Rahimi", "cashier", "Cashier@123", UserRole.Cashier, bTehran.Id, "cashier@zafaran.ir"),
            U("Maryam Ahmadi", "waiter", "Waiter@123", UserRole.Waiter, bTehran.Id, "waiter@zafaran.ir"),
            U("Reza Jafari", "waiter2", "Waiter@123", UserRole.Waiter, bKaraj.Id, "waiter2@zafaran.ir"),
            U("Hassan Gholi", "kitchen", "Kitchen@123", UserRole.Kitchen, bTehran.Id, "kitchen@zafaran.ir"),
            U("Fatemeh Naderi", "accountant", "Acc@123", UserRole.Accountant, bTehran.Id, "acc@zafaran.ir"),
            U("Ali Moradi", "inventory", "Stock@123", UserRole.Inventory, bTehran.Id, "stock@zafaran.ir"),
            U("Guest Customer", "customer", "Customer@123", UserRole.Customer, null, "guest@mail.com"));

        // ── Chart of accounts ─────────────────────────────────────
        Account A(string code, string name, int grp) => new() { Code = code, Name = name, Group = grp };
        var accCash = A("1100", "Cash & Bank (صندوق)", 1);
        var accAr = A("1200", "Accounts Receivable (online receivable)", 1);
        var accInventory = A("1300", "Inventory — Raw Materials", 1);
        var accVat = A("2100", "VAT Payable (value added tax)", 2);
        var accSales = A("4000", "Sales Revenue (sales)", 4);
        var accCogs = A("5000", "Cost of Goods Sold (cost of goods sold)", 5);
        var accOpex = A("6000", "Operating Expenses (operating expenses)", 5);
        db.Accounts.AddRange(accCash, accAr, accInventory, accVat, accSales, accCogs, accOpex);

        // ── Gateway configs (sandbox by default) ──────────────────
        foreach (var g in new[] { PaymentGatewayType.Zarinpal, PaymentGatewayType.Zibal, PaymentGatewayType.IDPay, PaymentGatewayType.PayIr, PaymentGatewayType.NextPay, PaymentGatewayType.Mellat, PaymentGatewayType.Saman, PaymentGatewayType.Parsian, PaymentGatewayType.Pasargad, PaymentGatewayType.Novin })
            db.GatewayConfigs.Add(new PaymentGatewayConfig { Gateway = g, Enabled = true, Sandbox = true, MerchantKey = g == PaymentGatewayType.Zarinpal ? "00000000-0000-0000-0000-000000000000" : null });

        // ── Ingredients ───────────────────────────────────────────
        Ingredient I(string name, string unit, decimal stock, decimal min, decimal cost, string supplier) =>
            new() { Name = name, Unit = unit, Stock = stock, MinStock = min, CostPerUnit = cost, SupplierName = supplier, BranchId = bTehran.Id };
        var beef = I("Beef Mince", "kg", 24, 8, 850_000, "Protein Master");
        var chicken = I("Chicken Breast", "kg", 18, 6, 480_000, "Protein Master");
        var lamb = I("Lamb Fillet", "kg", 9, 4, 1_650_000, "Protein Master");
        var rice = I("Rice (basmati)", "kg", 120, 30, 180_000, "Green Fields");
        var saffron = I("Saffron", "g", 220, 60, 1_200_000, "Khorasan Gold");
        var tomato = I("Tomato", "kg", 45, 12, 28_000, "Farm Fresh");
        var onion = I("Onion", "kg", 60, 15, 18_000, "Farm Fresh");
        var eggplant = I("Eggplant", "kg", 30, 10, 35_000, "Farm Fresh");
        var kashk = I("Kashk (whey)", "l", 12, 4, 240_000, "Dairy House");
        var walnut = I("Walnut", "kg", 8, 3, 1_100_000, "Nut Basket");
        var pomegranate = I("Pomegranate Paste", "l", 10, 3, 320_000, "Farm Fresh");
        var herbs = I("Mixed Fresh Herbs", "kg", 20, 6, 90_000, "Farm Fresh");
        var beans = I("Red Beans", "kg", 25, 8, 130_000, "Dry Goods Co.");
        var lime = I("Dried Lime", "pcs", 300, 80, 4_000, "Spice Route");
        var yogurt = I("Yogurt", "l", 40, 12, 65_000, "Dairy House");
        var bread = I("Sangak Bread", "pcs", 150, 40, 15_000, "Bakery Nejat");
        var oil = I("Frying Oil", "l", 55, 15, 120_000, "Dry Goods Co.");
        var cheese = I("Pizza Cheese", "kg", 14, 5, 420_000, "Dairy House");
        var burgerbun = I("Burger Buns", "pcs", 90, 30, 12_000, "Bakery Nejat");
        var potato = I("Potato", "kg", 70, 20, 22_000, "Farm Fresh");
        var tea = I("Saffron Tea Leaves", "kg", 6, 2, 1_800_000, "Spice Route");
        var icecream = I("Saffron Ice Cream", "l", 11, 4, 380_000, "Dairy House");
        var zoolbia = I("Zoolbia & Bamieh", "kg", 10, 3, 260_000, "Sweet House");
        var doogh = I("Doogh (yogurt drink)", "l", 36, 12, 45_000, "Dairy House");
        db.Ingredients.AddRange(beef, chicken, lamb, rice, saffron, tomato, onion, eggplant, kashk, walnut, pomegranate, herbs, beans, lime, yogurt, bread, oil, cheese, burgerbun, potato, tea, icecream, zoolbia, doogh);

        // ── Categories & Menu ─────────────────────────────────────
        Category C(string name, string emoji, int sort, Station st) => new() { Name = name, Emoji = emoji, SortOrder = sort, Station = st };
        var cStarters = C("Starters", "🥗", 1, Station.Cold);
        var cKebab = C("Kebabs", "🍢", 2, Station.Grill);
        var cStews = C("Stews", "🍲", 3, Station.HotKitchen);
        var cFast = C("Fast Food", "🍔", 4, Station.HotKitchen);
        var cDessert = C("Desserts", "🍮", 5, Station.Dessert);
        var cDrinks = C("Drinks", "🥤", 6, Station.Bar);
        db.Categories.AddRange(cStarters, cKebab, cStews, cFast, cDessert, cDrinks);

        MenuItem M(string name, string desc, decimal price, Category cat, int prep, int cal, bool spicy = false, bool veg = false) =>
            new() { Name = name, Description = desc, Price = price, Category = cat, PrepMinutes = prep, Calories = cal, IsSpicy = spicy, IsVegetarian = veg, BranchId = bTehran.Id };
        var mKashk = M("Kashk-e Bademjan", "Smoked eggplant, kashk, walnut, mint", 185_000, cStarters, 10, 320, veg: true);
        var mMirza = M("Mirza Ghasemi", "Garlicky eggplant, tomato, egg", 175_000, cStarters, 12, 290, veg: true);
        var mSalad = M("Shirazi Salad", "Cucumber, tomato, onion, verjuice", 95_000, cStarters, 6, 120, veg: true);
        var mKoobideh = M("Chelo Koobideh (2 skewers)", "Saffron rice, butter, grilled tomato", 385_000, cKebab, 22, 890);
        var mJujeh = M("Jujeh Kabab (boneless)", "Saffron-marinated chicken, rice", 365_000, cKebab, 20, 760);
        var mBarg = M("Kabab Barg", "Lamb fillet, saffron rice, grilled tomato", 495_000, cKebab, 25, 820);
        var mBakhtiari = M("Bakhtiari Kabab", "Lamb & chicken duo skewer", 465_000, cKebab, 24, 840);
        var mGhormeh = M("Ghormeh Sabzi", "Herb stew, red beans, dried lime", 295_000, cStews, 18, 610, spicy: true);
        var mGheyme = M("Gheyme Bademjan", "Split-pea & eggplant stew", 265_000, cStews, 16, 580);
        var mFesenjan = M("Fesenjan", "Walnut & pomegranate chicken stew", 385_000, cStews, 20, 720);
        var mBurger = M("Zafaran Burger", "Double beef patty, saffron aioli", 320_000, cFast, 14, 950);
        var mPizza = M("Margherita Pizza", "San Marzano, pizza cheese, basil", 298_000, cFast, 15, 880, veg: true);
        var mZoolbia = M("Zoolbia & Bamieh", "Saffron syrup-soaked pastry", 145_000, cDessert, 5, 430, veg: true);
        var mIce = M("Saffron Ice Cream", "Traditional bastani, pistachio", 135_000, cDessert, 4, 380, veg: true);
        var mDoogh = M("Doogh (bottled)", "Mint yogurt drink", 65_000, cDrinks, 2, 90, veg: true);
        var mTea = M("Saffron Tea Pot", "For two, with rock candy", 110_000, cDrinks, 5, 30, veg: true);
        db.MenuItems.AddRange(mKashk, mMirza, mSalad, mKoobideh, mJujeh, mBarg, mBakhtiari, mGhormeh, mGheyme, mFesenjan, mBurger, mPizza, mZoolbia, mIce, mDoogh, mTea);

        // ── Recipes (BOM) ─────────────────────────────────────────
        void R(MenuItem m, Ingredient ing, decimal qty) => db.RecipeItems.Add(new RecipeItem { MenuItem = m, Ingredient = ing, Quantity = qty });
        R(mKashk, eggplant, 0.25m); R(mKashk, kashk, 0.08m); R(mKashk, walnut, 0.02m); R(mKashk, oil, 0.03m);
        R(mMirza, eggplant, 0.22m); R(mMirza, tomato, 0.12m); R(mMirza, oil, 0.04m); R(mMirza, bread, 1);
        R(mSalad, tomato, 0.15m); R(mSalad, onion, 0.05m); R(mSalad, herbs, 0.03m);
        R(mKoobideh, beef, 0.30m); R(mKoobideh, rice, 0.35m); R(mKoobideh, saffron, 0.4m); R(mKoobideh, tomato, 0.15m);
        R(mJujeh, chicken, 0.28m); R(mJujeh, rice, 0.32m); R(mJujeh, saffron, 0.35m); R(mJujeh, yogurt, 0.05m);
        R(mBarg, lamb, 0.25m); R(mBarg, rice, 0.32m); R(mBarg, saffron, 0.4m); R(mBarg, tomato, 0.15m);
        R(mBakhtiari, lamb, 0.14m); R(mBakhtiari, chicken, 0.14m); R(mBakhtiari, rice, 0.32m); R(mBakhtiari, saffron, 0.35m);
        R(mGhormeh, herbs, 0.18m); R(mGhormeh, beans, 0.08m); R(mGhormeh, lime, 2); R(mGhormeh, beef, 0.15m); R(mGhormeh, rice, 0.30m);
        R(mGheyme, beans, 0.07m); R(mGheyme, eggplant, 0.15m); R(mGheyme, tomato, 0.12m); R(mGheyme, rice, 0.30m);
        R(mFesenjan, walnut, 0.12m); R(mFesenjan, pomegranate, 0.08m); R(mFesenjan, chicken, 0.22m); R(mFesenjan, rice, 0.28m);
        R(mBurger, beef, 0.24m); R(mBurger, burgerbun, 1); R(mBurger, cheese, 0.04m); R(mBurger, potato, 0.15m); R(mBurger, oil, 0.05m);
        R(mPizza, cheese, 0.14m); R(mPizza, tomato, 0.10m); R(mPizza, oil, 0.03m);
        R(mZoolbia, zoolbia, 0.20m);
        R(mIce, icecream, 0.18m);
        R(mDoogh, doogh, 0.33m);
        R(mTea, tea, 0.012m);

        // ── Tables ────────────────────────────────────────────────
        for (int i = 1; i <= 12; i++) db.Tables.Add(new DiningTable { Number = i, Seats = i % 3 == 0 ? 6 : 4, Status = TableStatus.Free, QrToken = Guid.NewGuid().ToString("N")[..10], BranchId = bTehran.Id });
        for (int i = 1; i <= 8; i++) db.Tables.Add(new DiningTable { Number = i, Seats = 4, Status = TableStatus.Free, QrToken = Guid.NewGuid().ToString("N")[..10], BranchId = bKaraj.Id });

        // ── Cameras (demo streams render server-side — zero hardware needed) ──
        db.Cameras.AddRange(
            new Camera { Name = "Kitchen — Grill Line", Type = CameraType.Demo, Url = "demo:grill", Location = "Main kitchen", BranchId = bTehran.Id },
            new Camera { Name = "Dining Hall — Entrance", Type = CameraType.Demo, Url = "demo:hall", Location = "Hall A", BranchId = bTehran.Id },
            new Camera { Name = "Cashier Counter", Type = CameraType.Demo, Url = "demo:cashier", Location = "Front desk", BranchId = bTehran.Id },
            new Camera { Name = "Storage Room", Type = CameraType.Demo, Url = "demo:storage", Location = "Back store", BranchId = bTehran.Id });

        // ── Printers (documented defaults; point to your LAN printer IP) ──
        db.Printers.AddRange(
            new PrinterConfig { Name = "Kitchen Thermal (80mm)", Type = PrinterType.Kitchen, Host = "127.0.0.1", Port = 9100, IsDefault = true, UseRasterMode = true, BranchId = bTehran.Id },
            new PrinterConfig { Name = "Cashier Receipt (80mm)", Type = PrinterType.Receipt, Host = "127.0.0.1", Port = 9101, IsDefault = true, UseRasterMode = true, BranchId = bTehran.Id });

        // ── Running event ─────────────────────────────────────────
        db.Events.Add(new RestaurantEvent
        {
            Title = "Yalda Night Special",
            Description = "15% off all kebabs — longest night of the year at Zafaran!",
            StartAt = DateTime.UtcNow.AddDays(-1), EndAt = DateTime.UtcNow.AddDays(7),
            DiscountPercent = 15, BannerEmoji = "🎉", BranchId = null
        });

        // ── Butter ingredient (used by Koobideh) ──
        var butter = I("Butter", "kg", 10, 3, 850_000, "Dairy House");
        db.Ingredients.Add(butter);
        db.RecipeItems.Add(new RecipeItem { MenuItem = mKoobideh, Ingredient = butter, Quantity = 0.02m });

        await db.SaveChangesAsync();

        // Shared catalog: menu + ingredients available to all branches in the demo
        foreach (var item in db.MenuItems.Where(x => x.BranchId == bTehran.Id).ToList()) item.BranchId = null;
        db.Ingredients.Where(x => x.BranchId == bTehran.Id).ToList().ForEach(x => x.BranchId = null);
        await db.SaveChangesAsync();
    }
}
