using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Security;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Infrastructure.Data;

/// <summary>Ensures the database exists and seeds a fully demo-ready restaurant — تمام داده‌ها به فارسی.</summary>
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return; // already seeded

        // ── Settings ──────────────────────────────────────────────
        db.Settings.AddRange(
            new AppSetting { Key = "Restaurant:Name", Value = "رستوران زعفران", Description = "نام برند روی رسید و وب‌سایت" },
            new AppSetting { Key = "Restaurant:TaxRatePercent", Value = "9", Description = "درصد ارزش افزوده" },
            new AppSetting { Key = "Restaurant:Currency", Value = "تومان", Description = "واحد پول نمایشی" },
            new AppSetting { Key = "Receipt:Footer", Value = "نوش جان — با تشکر از انتخاب شما", Description = "پانوشت چاپی رسید" },
            new AppSetting { Key = "Kds:LateMinutes", Value = "15", Description = "بعد از این تعداد دقیقه، تیکت قرمز می‌شود" });

        // ── Branches ──────────────────────────────────────────────
        var bTehran = new Branch { Name = "زعفران تهران — شعبه مرکزی", City = "تهران", Address = "خیابان ولیعصر، پلاک ۱۲۴", Phone = "021-22334455" };
        var bKaraj = new Branch { Name = "زعفران کرج", City = "کرج", Address = "جاده چالوس، مهرشهر", Phone = "026-33445566" };
        var bOnline = new Branch { Name = "آشپزخانه ابری — مرکز پیک", City = "تهران", Address = "سعادت‌آباد", Phone = "021-88776655" };
        db.Branches.AddRange(bTehran, bKaraj, bOnline);

        // ── Users (همه رمزها در README) ────────────────────────────
        var hasher = new Pbkdf2PasswordHasher();
        AppUser U(string full, string userName, string pwd, UserRole role, Guid? branch, string email) =>
            new() { FullName = full, UserName = userName, Email = email, Phone = "0912" + Random.Shared.Next(1000000, 9999999), PasswordHash = hasher.Hash(pwd), Role = role, BranchId = branch };
        db.Users.AddRange(
            U("سارا سیفی", "superadmin", "Admin@123", UserRole.SuperAdmin, null, "s.seify1999@gmail.com"),
            U("نیما حسینی", "manager", "Manager@123", UserRole.Manager, bTehran.Id, "manager@zafaran.ir"),
            U("لیلا کریمی", "manager2", "Manager@123", UserRole.Manager, bKaraj.Id, "manager2@zafaran.ir"),
            U("امید رحیمی", "cashier", "Cashier@123", UserRole.Cashier, bTehran.Id, "cashier@zafaran.ir"),
            U("مریم احمدی", "waiter", "Waiter@123", UserRole.Waiter, bTehran.Id, "waiter@zafaran.ir"),
            U("رضا جعفری", "waiter2", "Waiter@123", UserRole.Waiter, bKaraj.Id, "waiter2@zafaran.ir"),
            U("حسن قلی‌زاده", "kitchen", "Kitchen@123", UserRole.Kitchen, bTehran.Id, "kitchen@zafaran.ir"),
            U("فاطمه نادری", "accountant", "Acc@123", UserRole.Accountant, bTehran.Id, "acc@zafaran.ir"),
            U("علی مرادی", "inventory", "Stock@123", UserRole.Inventory, bTehran.Id, "stock@zafaran.ir"),
            U("مشتری مهمان", "customer", "Customer@123", UserRole.Customer, null, "guest@mail.com"));

        // ── Chart of accounts — کدینگ حساب‌ها ──────────────────────
        Account A(string code, string name, int grp) => new() { Code = code, Name = name, Group = grp };
        var accCash = A("1100", "صندوق و بانک", 1);
        var accAr = A("1200", "حساب‌های دریافتنی (فروش آنلاین)", 1);
        var accInventory = A("1300", "موجودی مواد اولیه", 1);
        var accVat = A("2100", "مالیات بر ارزش افزوده پرداختنی", 2);
        var accSales = A("4000", "درآمد فروش", 4);
        var accCogs = A("5000", "بهای تمام‌شده کالای فروخته‌شده", 5);
        var accOpex = A("6000", "هزینه‌های عملیاتی", 5);
        db.Accounts.AddRange(accCash, accAr, accInventory, accVat, accSales, accCogs, accOpex);

        // ── Gateway configs (پیش‌فرض سندباکس) ──────────────────────
        foreach (var g in new[] { PaymentGatewayType.Zarinpal, PaymentGatewayType.Zibal, PaymentGatewayType.IDPay, PaymentGatewayType.PayIr, PaymentGatewayType.NextPay, PaymentGatewayType.Mellat, PaymentGatewayType.Saman, PaymentGatewayType.Parsian, PaymentGatewayType.Pasargad, PaymentGatewayType.Novin })
            db.GatewayConfigs.Add(new PaymentGatewayConfig { Gateway = g, Enabled = true, Sandbox = true, MerchantKey = g == PaymentGatewayType.Zarinpal ? "00000000-0000-0000-0000-000000000000" : null });

        // ── Ingredients — مواد اولیه ─────────────────────────────
        Ingredient I(string name, string unit, decimal stock, decimal min, decimal cost, string supplier) =>
            new() { Name = name, Unit = unit, Stock = stock, MinStock = min, CostPerUnit = cost, SupplierName = supplier, BranchId = bTehran.Id };
        var beef = I("گوشت چرخ‌کرده گوساله", "کیلوگرم", 24, 8, 850_000, "پروتئین مستر");
        var chicken = I("سینه مرغ", "کیلوگرم", 18, 6, 480_000, "پروتئین مستر");
        var lamb = I("فیله گوشت بره", "کیلوگرم", 9, 4, 1_650_000, "پروتئین مستر");
        var rice = I("برنج ایرانی (طارم)", "کیلوگرم", 120, 30, 180_000, "زرین دشت");
        var saffron = I("زعفران سرگل", "گرم", 220, 60, 1_200_000, "طلای خراسان");
        var tomato = I("گوجه‌فرنگی", "کیلوگرم", 45, 12, 28_000, "تازه‌رسان مزرعه");
        var onion = I("پیاز", "کیلوگرم", 60, 15, 18_000, "تازه‌رسان مزرعه");
        var eggplant = I("بادمجان", "کیلوگرم", 30, 10, 35_000, "تازه‌رسان مزرعه");
        var kashk = I("کشک", "لیتر", 12, 4, 240_000, "خانه لبنیات");
        var walnut = I("گردوی مقطوع", "کیلوگرم", 8, 3, 1_100_000, "سبد خشکبار");
        var pomegranate = I("رب انار", "لیتر", 10, 3, 320_000, "تازه‌رسان مزرعه");
        var herbs = I("سبزی معطر تازه (ترکیبی)", "کیلوگرم", 20, 6, 90_000, "تازه‌رسان مزرعه");
        var beans = I("لوبیا قرمز", "کیلوگرم", 25, 8, 130_000, "پخش حبوبات");
        var lime = I("لیموعمانی", "عدد", 300, 80, 4_000, "ادویه راه ابریشم");
        var yogurt = I("ماست سنتی", "لیتر", 40, 12, 65_000, "خانه لبنیات");
        var bread = I("نان سنگک", "عدد", 150, 40, 15_000, "نانوایی نجات");
        var oil = I("روغن سرخ‌کردنی", "لیتر", 55, 15, 120_000, "پخش حبوبات");
        var cheese = I("پنیر پیتزا", "کیلوگرم", 14, 5, 420_000, "خانه لبنیات");
        var burgerbun = I("نان برگر", "عدد", 90, 30, 12_000, "نانوایی نجات");
        var potato = I("سیب‌زمینی", "کیلوگرم", 70, 20, 22_000, "تازه‌رسان مزرعه");
        var tea = I("چای سیاه ممتاز", "کیلوگرم", 6, 2, 1_800_000, "ادویه راه ابریشم");
        var icecream = I("بستنی زعفرانی سنتی", "لیتر", 11, 4, 380_000, "خانه لبنیات");
        var zoolbia = I("زولبیا و بامیه", "کیلوگرم", 10, 3, 260_000, "شیرینی‌سرای یاس");
        var doogh = I("دوغ محلی", "لیتر", 36, 12, 45_000, "خانه لبنیات");
        db.Ingredients.AddRange(beef, chicken, lamb, rice, saffron, tomato, onion, eggplant, kashk, walnut, pomegranate, herbs, beans, lime, yogurt, bread, oil, cheese, burgerbun, potato, tea, icecream, zoolbia, doogh);

        // ── Categories & Menu — دسته‌ها و منو ─────────────────────
        Category C(string name, string emoji, int sort, Station st) => new() { Name = name, Emoji = emoji, SortOrder = sort, Station = st };
        var cStarters = C("پیش‌غذا", "🥗", 1, Station.Cold);
        var cKebab = C("کباب‌ها", "🍢", 2, Station.Grill);
        var cStews = C("خورشت‌ها", "🍲", 3, Station.HotKitchen);
        var cFast = C("فست‌فود", "🍔", 4, Station.HotKitchen);
        var cDessert = C("دسر و شیرینی", "🍮", 5, Station.Dessert);
        var cDrinks = C("نوشیدنی‌ها", "🥤", 6, Station.Bar);
        db.Categories.AddRange(cStarters, cKebab, cStews, cFast, cDessert, cDrinks);

        MenuItem M(string name, string desc, decimal price, Category cat, int prep, int cal, bool spicy = false, bool veg = false) =>
            new() { Name = name, Description = desc, Price = price, Category = cat, PrepMinutes = prep, Calories = cal, IsSpicy = spicy, IsVegetarian = veg, BranchId = bTehran.Id };
        var mKashk = M("کشک بادمجان", "بادمجان دودی، کشک اصل، گردو و نعناع داغ", 185_000, cStarters, 10, 320, veg: true);
        var mMirza = M("میرزا قاسمی", "بادمجان سیر‌داده، گوجه و تخم‌مرغ محلی", 175_000, cStarters, 12, 290, veg: true);
        var mSalad = M("سالاد شیرازی", "خیار، گوجه، پیاز و آبغوره طبیعی", 95_000, cStarters, 6, 120, veg: true);
        var mKoobideh = M("چلو کباب کوبیده (دو سیخ)", "برنج زعفرانی، کره و گوجه کبابی", 385_000, cKebab, 22, 890);
        var mJujeh = M("جوجه کباب بی‌استخوان", "مرغ زعفرانی‌مارینیت‌شده با برنج ایرانی", 365_000, cKebab, 20, 760);
        var mBarg = M("کباب برگ", "فیله بره، برنج زعفرانی و گوجه کبابی", 495_000, cKebab, 25, 820);
        var mBakhtiari = M("کباب بختیاری", "ترکیب فیله بره و مرغ زعفرانی", 465_000, cKebab, 24, 840);
        var mGhormeh = M("خورشت قورمه سبزی", "سبزی معطر، لوبیا قرمز و لیموعمانی", 295_000, cStews, 18, 610, spicy: true);
        var mGheyme = M("خورشت قیمه بادمجان", "لپه، بادمجان سرخ‌شده و سیب‌زمینی", 265_000, cStews, 16, 580);
        var mFesenjan = M("خورشت فسنجان", "گردوی مقطوع و رب انار با مرغ", 385_000, cStews, 20, 720);
        var mBurger = M("برگر زعفران", "دو لایه گوشت گوساله با سس سیر زعفرانی", 320_000, cFast, 14, 950);
        var mPizza = M("پیتزا مارگاریتا", "سس گوجه ایتالیایی، پنیر پیتزا و ریحان تازه", 298_000, cFast, 15, 880, veg: true);
        var mZoolbia = M("زولبیا و بامیه", "شیرینی سنتی با شربت زعفران", 145_000, cDessert, 5, 430, veg: true);
        var mIce = M("بستنی زعفرانی", "بستنی سنتی با خلال پسته", 135_000, cDessert, 4, 380, veg: true);
        var mDoogh = M("دوغ گازدار", "دوغ محلی با نعناع", 65_000, cDrinks, 2, 90, veg: true);
        var mTea = M("چای زعفرانی (قوری)", "مخصوص دو نفر همراه نبات", 110_000, cDrinks, 5, 30, veg: true);
        db.MenuItems.AddRange(mKashk, mMirza, mSalad, mKoobideh, mJujeh, mBarg, mBakhtiari, mGhormeh, mGheyme, mFesenjan, mBurger, mPizza, mZoolbia, mIce, mDoogh, mTea);

        // ── Recipes (BOM) — رسپی و مواد اولیه ─────────────────────
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

        // ── Tables — میزها ────────────────────────────────────────
        for (int i = 1; i <= 12; i++) db.Tables.Add(new DiningTable { Number = i, Seats = i % 3 == 0 ? 6 : 4, Status = TableStatus.Free, QrToken = Guid.NewGuid().ToString("N")[..10], BranchId = bTehran.Id });
        for (int i = 1; i <= 8; i++) db.Tables.Add(new DiningTable { Number = i, Seats = 4, Status = TableStatus.Free, QrToken = Guid.NewGuid().ToString("N")[..10], BranchId = bKaraj.Id });

        // ── Cameras — دوربین‌ها (استریم نمایشی سمت سرور، بدون نیاز به سخت‌افزار) ──
        db.Cameras.AddRange(
            new Camera { Name = "آشپزخانه — خط کباب", Type = CameraType.Demo, Url = "demo:grill", Location = "آشپزخانه اصلی", BranchId = bTehran.Id },
            new Camera { Name = "سالن — ورودی اصلی", Type = CameraType.Demo, Url = "demo:hall", Location = "سالن A", BranchId = bTehran.Id },
            new Camera { Name = "صندوق — پیشخوان", Type = CameraType.Demo, Url = "demo:cashier", Location = "فضای پذیرش", BranchId = bTehran.Id },
            new Camera { Name = "انبار مواد", Type = CameraType.Demo, Url = "demo:storage", Location = "انبار پشتی", BranchId = bTehran.Id });

        // ── Printers — چاپگرها (IP چاپگر شبکه خود را تنظیم کنید) ──
        db.Printers.AddRange(
            new PrinterConfig { Name = "چاپگر حرارتی آشپزخانه (۸۰mm)", Type = PrinterType.Kitchen, Host = "127.0.0.1", Port = 9100, IsDefault = true, UseRasterMode = true, BranchId = bTehran.Id },
            new PrinterConfig { Name = "چاپگر رسید صندوق (۸۰mm)", Type = PrinterType.Receipt, Host = "127.0.0.1", Port = 9101, IsDefault = true, UseRasterMode = true, BranchId = bTehran.Id });

        // ── Running event — رویداد فعال ───────────────────────────
        db.Events.Add(new RestaurantEvent
        {
            Title = "جشنواره شب یلدا",
            Description = "۱۵٪ تخفیف روی همه کباب‌ها — بلندترین شب سال در زعفران!",
            StartAt = DateTime.UtcNow.AddDays(-1), EndAt = DateTime.UtcNow.AddDays(7),
            DiscountPercent = 15, BannerEmoji = "🎉", BranchId = null
        });

        // ── Butter — کره (مورد استفاده چلوکباب) ──
        var butter = I("کره حیوانی", "کیلوگرم", 10, 3, 850_000, "خانه لبنیات");
        db.Ingredients.Add(butter);
        db.RecipeItems.Add(new RecipeItem { MenuItem = mKoobideh, Ingredient = butter, Quantity = 0.02m });

        await db.SaveChangesAsync();

        // Shared catalog: menu + ingredients available to all branches in the demo
        foreach (var item in db.MenuItems.Where(x => x.BranchId == bTehran.Id).ToList()) item.BranchId = null;
        db.Ingredients.Where(x => x.BranchId == bTehran.Id).ToList().ForEach(x => x.BranchId = null);
        await db.SaveChangesAsync();
    }
}
