using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Domain.Entities.Accounting;
using SmartRestaurant.Domain.Entities.Customers;
using SmartRestaurant.Domain.Entities.Events;
using SmartRestaurant.Domain.Entities.Identity;
using SmartRestaurant.Domain.Entities.Inventory;
using SmartRestaurant.Domain.Entities.Menu;
using SmartRestaurant.Domain.Entities.Notifications;
using SmartRestaurant.Domain.Entities.Orders;
using SmartRestaurant.Domain.Entities.Reservations;
using SmartRestaurant.Domain.Entities.Settings;
using SmartRestaurant.Domain.Entities.Tables;
using System.Security.Cryptography;

namespace SmartRestaurant.Infrastructure.Data;

/// <summary>Creates the database, seeds demo users for every role and rich sample data.</summary>
public static class DbSeeder
{
    private static string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password);

    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();
        if (await db.Users.AnyAsync()) return; // already seeded

        // ---------- Users ----------
        var users = new List<AppUser>
        {
            new() { FullName = "Sara Seify", Email = "superadmin@restaurant.com", Phone = "+1-555-0001", PasswordHash = Hash("Super@123"), Role = AppRole.SuperAdmin, JobTitle = "System Owner", PinCode = "1111", HireDate = DateTime.UtcNow.AddYears(-3), BaseSalary = 9500m },
            new() { FullName = "Daniel Moore", Email = "manager@restaurant.com", Phone = "+1-555-0002", PasswordHash = Hash("Manager@123"), Role = AppRole.Manager, JobTitle = "General Manager", PinCode = "2222", HireDate = DateTime.UtcNow.AddYears(-2), BaseSalary = 6200m },
            new() { FullName = "Olivia Cash", Email = "cashier@restaurant.com", Phone = "+1-555-0003", PasswordHash = Hash("Cashier@123"), Role = AppRole.Cashier, JobTitle = "Head Cashier", PinCode = "3333", HireDate = DateTime.UtcNow.AddYears(-1), BaseSalary = 3200m },
            new() { FullName = "Liam Walker", Email = "waiter@restaurant.com", Phone = "+1-555-0004", PasswordHash = Hash("Waiter@123"), Role = AppRole.Waiter, JobTitle = "Senior Waiter", PinCode = "4444", HireDate = DateTime.UtcNow.AddMonths(-8), BaseSalary = 2400m },
            new() { FullName = "Mia Laurent", Email = "waiter2@restaurant.com", Phone = "+1-555-0009", PasswordHash = Hash("Waiter@123"), Role = AppRole.Waiter, JobTitle = "Waiter", PinCode = "4445", HireDate = DateTime.UtcNow.AddMonths(-4), BaseSalary = 2200m },
            new() { FullName = "Chef Arman Rossi", Email = "chef@restaurant.com", Phone = "+1-555-0005", PasswordHash = Hash("Chef@123"), Role = AppRole.Chef, JobTitle = "Executive Chef", PinCode = "5555", HireDate = DateTime.UtcNow.AddYears(-4), BaseSalary = 7400m },
            new() { FullName = "Chef Kim Park", Email = "chef2@restaurant.com", Phone = "+1-555-0010", PasswordHash = Hash("Chef@123"), Role = AppRole.Chef, JobTitle = "Pastry Chef", PinCode = "5556", HireDate = DateTime.UtcNow.AddYears(-1), BaseSalary = 4100m },
            new() { FullName = "Grace Ledger", Email = "accountant@restaurant.com", Phone = "+1-555-0006", PasswordHash = Hash("Account@123"), Role = AppRole.Accountant, JobTitle = "Financial Controller", PinCode = "6666", HireDate = DateTime.UtcNow.AddYears(-2), BaseSalary = 5200m },
            new() { FullName = "Chris Guest", Email = "guest@restaurant.com", Phone = "+1-555-0007", PasswordHash = Hash("Guest@123"), Role = AppRole.Customer, PinCode = null },
        };
        db.Users.AddRange(users);

        // ---------- Settings ----------
        db.Settings.Add(new RestaurantSetting
        {
            Name = "Aurora Smart Restaurant",
            Tagline = "Fine dining, intelligently served",
            Address = "128 Gourmet Avenue, Downtown District",
            Phone = "+1-555-AURORA",
            Email = "hello@aurora-restaurant.com",
            OpeningHours = "10:00 - 23:30",
        });
        await db.SaveChangesAsync(); // assign identity ids before children reference them

        // ---------- Tables ----------
        var qrSeed = 1000;
        var tables = new List<DiningTable>();
        for (int i = 1; i <= 12; i++)
            tables.Add(new() { Number = i, Section = i <= 7 ? TableSection.MainHall : i <= 9 ? TableSection.Terrace : TableSection.VIP, Capacity = i <= 8 ? 4 : 6, QrToken = $"T{i}-{qrSeed + i}" });
        tables.Add(new() { Number = 13, Section = TableSection.Private, Capacity = 14, QrToken = $"T13-{qrSeed + 13}", Notes = "Private dining room — events" });
        db.Tables.AddRange(tables);
        await db.SaveChangesAsync();

        // ---------- Menu ----------
        var cats = new List<Category>
        {
            new() { Name = "Starters", Description = "Begin your journey", SortOrder = 1 },
            new() { Name = "Main Courses", Description = "Signature plates", SortOrder = 2 },
            new() { Name = "Pizza & Pasta", Description = "Italian classics", SortOrder = 3 },
            new() { Name = "Desserts", Description = "Sweet endings", SortOrder = 4 },
            new() { Name = "Drinks", Description = "Crafted beverages", SortOrder = 5 },
        };
        db.Categories.AddRange(cats);
        await db.SaveChangesAsync();

        var items = new List<MenuItem>
        {
            new() { CategoryId = 1, Name = "Truffle Bruschetta", Description = "Artisan sourdough, black truffle paste, burrata, basil oil.", Price = 11.90m, Cost = 3.2m, PrepTimeMinutes = 8, Calories = 320, IsVegetarian = true, Station = PrepStation.Cold, IsFeatured = true, ImageUrl = "/assets/img/bruschetta.svg" },
            new() { CategoryId = 1, Name = "Crispy Calamari", Description = "Golden squid rings, saffron aioli, charred lemon.", Price = 13.50m, Cost = 4.6m, PrepTimeMinutes = 10, Calories = 410, Station = PrepStation.Fry, Allergens = "seafood,gluten", ImageUrl = "/assets/img/calamari.svg" },
            new() { CategoryId = 1, Name = "Garden Spring Rolls", Description = "Rice paper, mint, carrot, glass noodles, peanut dip.", Price = 8.90m, Cost = 2.1m, PrepTimeMinutes = 7, Calories = 210, IsVegetarian = true, IsGlutenFree = true, Station = PrepStation.Cold, ImageUrl = "/assets/img/springrolls.svg" },
            new() { CategoryId = 2, Name = "Wagyu Ribeye 300g", Description = "MBS 8-9 ribeye, smoked bone marrow butter, rosemary jus.", Price = 58.00m, Cost = 24m, PrepTimeMinutes = 25, Calories = 890, Station = PrepStation.Grill, IsFeatured = true, Allergens = "dairy", ImageUrl = "/assets/img/steak.svg" },
            new() { CategoryId = 2, Name = "Herb-Crusted Salmon", Description = "Atlantic salmon, dill crust, lemon beurre blanc, asparagus.", Price = 27.50m, Cost = 9.8m, PrepTimeMinutes = 18, Calories = 640, Station = PrepStation.Grill, IsGlutenFree = true, ImageUrl = "/assets/img/salmon.svg" },
            new() { CategoryId = 2, Name = "Lamb Shank Borguise", Description = "Slow-braised 6h, red wine reduction, truffle mash.", Price = 32.00m, Cost = 11.5m, PrepTimeMinutes = 22, Calories = 780, Station = PrepStation.HotLine, IsSpicy = true, ImageUrl = "/assets/img/lamb.svg" },
            new() { CategoryId = 2, Name = "Buddha Power Bowl", Description = "Quinoa, avocado, edamame, roasted chickpeas, tahini.", Price = 16.90m, Cost = 4.2m, PrepTimeMinutes = 10, Calories = 520, IsVegetarian = true, IsGlutenFree = true, Station = PrepStation.Cold, ImageUrl = "/assets/img/bowl.svg" },
            new() { CategoryId = 3, Name = "Margherita Reale", Description = "San Marzano DOP, fior di latte, basil, EVOO. Wood-fired.", Price = 14.90m, Cost = 3.4m, PrepTimeMinutes = 12, Calories = 780, IsVegetarian = true, Station = PrepStation.HotLine, ImageUrl = "/assets/img/pizza.svg" },
            new() { CategoryId = 3, Name = "Truffle Funghi Pizza", Description = "Wild mushrooms, truffle cream, taleggio, thyme.", Price = 19.50m, Cost = 5.6m, PrepTimeMinutes = 13, Calories = 860, IsVegetarian = true, Station = PrepStation.HotLine, IsFeatured = true, ImageUrl = "/assets/img/pizza2.svg" },
            new() { CategoryId = 3, Name = "Tagliatelle Alfredo", Description = "Fresh egg pasta, parmesan cream, cracked pepper.", Price = 17.80m, Cost = 4.1m, PrepTimeMinutes = 14, Calories = 720, IsVegetarian = true, Station = PrepStation.HotLine, Allergens = "gluten,dairy,eggs", ImageUrl = "/assets/img/pasta.svg" },
            new() { CategoryId = 4, Name = "Molten Lava Cake", Description = "70% dark chocolate, vanilla gelato, raspberry coulis.", Price = 9.90m, Cost = 2.4m, PrepTimeMinutes = 12, Calories = 480, IsVegetarian = true, Station = PrepStation.Pastry, IsFeatured = true, ImageUrl = "/assets/img/lava.svg" },
            new() { CategoryId = 4, Name = "Pistachio Tiramisu", Description = "Sicilian pistachio, mascarpone, espresso-soaked savoiardi.", Price = 8.50m, Cost = 2.2m, PrepTimeMinutes = 5, Calories = 430, IsVegetarian = true, Station = PrepStation.Pastry, ImageUrl = "/assets/img/tiramisu.svg" },
            new() { CategoryId = 5, Name = "Signature Aurora Mocktail", Description = "Passion fruit, elderflower, mint, sparkling water.", Price = 7.50m, Cost = 1.6m, PrepTimeMinutes = 4, Calories = 120, IsVegetarian = true, Station = PrepStation.Bar, ImageUrl = "/assets/img/mocktail.svg" },
            new() { CategoryId = 5, Name = "Single-Origin Espresso", Description = "Ethiopian Yirgacheffe, honeyed citrus notes.", Price = 3.80m, Cost = 0.6m, PrepTimeMinutes = 3, Calories = 5, Station = PrepStation.Bar, ImageUrl = "/assets/img/espresso.svg" },
            new() { CategoryId = 5, Name = "Fresh Orange Juice", Description = "Cold-pressed, nothing added.", Price = 5.20m, Cost = 1.2m, PrepTimeMinutes = 3, Calories = 110, IsVegetarian = true, IsGlutenFree = true, Station = PrepStation.Bar, ImageUrl = "/assets/img/juice.svg" },
        };
        // popularity counters for realistic analytics
        var rnd = new Random(42);
        foreach (var it in items) it.TimesOrdered = rnd.Next(18, 240);
        db.MenuItems.AddRange(items);
        db.ModifierGroups.AddRange(
            new ModifierGroup { MenuItemId = 8, Name = "Extra Toppings", MinSelection = 0, MaxSelection = 4, Options = { new() { Name = "Mozzarella", ExtraPrice = 1.8m }, new() { Name = "Basil", ExtraPrice = 0.5m }, new() { Name = "Olives", ExtraPrice = 1.2m } } },
            new ModifierGroup { MenuItemId = 4, Name = "Doneness", MinSelection = 1, MaxSelection = 1, Options = { new() { Name = "Rare" }, new() { Name = "Medium Rare" }, new() { Name = "Medium" }, new() { Name = "Well Done" } } },
            new ModifierGroup { MenuItemId = 13, Name = "Ice Level", MinSelection = 1, MaxSelection = 1, Options = { new() { Name = "Normal Ice" }, new() { Name = "Less Ice" }, new() { Name = "No Ice" } } }
        );

        // ---------- Suppliers & Ingredients ----------
        var suppliers = new List<Supplier>
        {
            new() { Name = "Prime Meats Co.", ContactName = "Tony Esposito", Phone = "+1-555-9001", Email = "sales@primemeats.com", Rating = 4.8 },
            new() { Name = "Green Leaf Farms", ContactName = "Hana Suzuki", Phone = "+1-555-9002", Email = "orders@greenleaf.farm", Rating = 4.5 },
            new() { Name = "Ocean Catch Ltd.", ContactName = "Marco Blau", Phone = "+1-555-9003", Email = "hello@oceancatch.io", Rating = 4.2 },
            new() { Name = "Dairy Valley", ContactName = "Elsa Nord", Phone = "+1-555-9004", Email = "b2b@dairyvalley.com", Rating = 4.0 },
        };
        db.Suppliers.AddRange(suppliers);
        await db.SaveChangesAsync();

        db.Ingredients.AddRange(
            new() { Name = "Wagyu Ribeye", Unit = "kg", StockQty = 14.5m, MinStock = 8m, CostPerUnit = 78m, SupplierId = 1, Storage = StorageType.Freezer },
            new() { Name = "Atlantic Salmon Fillet", Unit = "kg", StockQty = 9.2m, MinStock = 10m, CostPerUnit = 22m, SupplierId = 3, Storage = StorageType.Fridge },
            new() { Name = "Lamb Shank", Unit = "kg", StockQty = 12m, MinStock = 6m, CostPerUnit = 18m, SupplierId = 1, Storage = StorageType.Freezer },
            new() { Name = "Fior di Latte", Unit = "kg", StockQty = 6.5m, MinStock = 7m, CostPerUnit = 9m, SupplierId = 4, Storage = StorageType.Fridge },
            new() { Name = "San Marzano Tomato", Unit = "kg", StockQty = 21m, MinStock = 10m, CostPerUnit = 4.2m, SupplierId = 2, Storage = StorageType.Dry },
            new() { Name = "Black Truffle Paste", Unit = "g", StockQty = 380m, MinStock = 300m, CostPerUnit = 0.9m, SupplierId = 2, Storage = StorageType.Fridge },
            new() { Name = "Burrata", Unit = "pcs", StockQty = 18m, MinStock = 15m, CostPerUnit = 3.8m, SupplierId = 4, Storage = StorageType.Fridge },
            new() { Name = "Espresso Beans", Unit = "kg", StockQty = 11m, MinStock = 5m, CostPerUnit = 26m, SupplierId = 2, Storage = StorageType.Dry },
            new() { Name = "Dark Chocolate 70%", Unit = "kg", StockQty = 4.5m, MinStock = 5m, CostPerUnit = 14m, SupplierId = 2, Storage = StorageType.Fridge },
            new() { Name = "Fresh Basil", Unit = "kg", StockQty = 2.1m, MinStock = 1.5m, CostPerUnit = 12m, SupplierId = 2, Storage = StorageType.Fridge }
        );
        await db.SaveChangesAsync();

        // ---------- Purchase Orders ----------
        db.PurchaseOrders.Add(new PurchaseOrder
        {
            PoNumber = "PO-2026-0001", SupplierId = 1, Status = PurchaseStatus.Received, ExpectedDate = DateTime.UtcNow.AddDays(-2),
            Notes = "Weekly protein restock", CreatedById = 2,
            Items = { new() { IngredientId = 1, Quantity = 10m, UnitCost = 76m }, new() { IngredientId = 3, Quantity = 8m, UnitCost = 17.2m } },
            Total = 897.6m
        });
        await db.SaveChangesAsync();

        // ---------- Customers ----------
        db.Customers.AddRange(
            new() { FullName = "Emily Carter", Phone = "+1-555-7001", Email = "emily@mail.com", LoyaltyPoints = 2340, TotalSpent = 2340m, OrderCount = 31, BirthDate = new DateTime(1993, 4, 12) },
            new() { FullName = "James Wu", Phone = "+1-555-7002", Email = "james.wu@mail.com", LoyaltyPoints = 1180, TotalSpent = 1180m, OrderCount = 17 },
            new() { FullName = "Sofia Rossi", Phone = "+1-555-7003", Email = "sofia@mail.com", LoyaltyPoints = 520, TotalSpent = 520m, OrderCount = 8 },
            new() { FullName = "Omar Hassan", Phone = "+1-555-7004", Email = "omar@mail.com", LoyaltyPoints = 150, TotalSpent = 150m, OrderCount = 2 }
        );
        await db.SaveChangesAsync();

        // ---------- Reservations ----------
        db.Reservations.AddRange(
            new() { CustomerName = "Emily Carter", Phone = "+1-555-7001", PartySize = 4, TableId = 3, DateTime = DateTime.UtcNow.AddHours(3), Status = ReservationStatus.Confirmed, Occasion = "Anniversary", Source = "Website" },
            new() { CustomerName = "The Henderson Family", Phone = "+1-555-7100", PartySize = 8, TableId = 13, DateTime = DateTime.UtcNow.AddHours(6), Status = ReservationStatus.Pending, Occasion = "Reunion", Source = "Phone" },
            new() { CustomerName = "James Wu", Phone = "+1-555-7002", PartySize = 2, TableId = 8, DateTime = DateTime.UtcNow.AddDays(-1).AddHours(-2), Status = ReservationStatus.Seated, Source = "Website" }
        );
        await db.SaveChangesAsync();

        // ---------- Event packages & bookings ----------
        db.EventPackages.AddRange(
            new() { Name = "Silver Wedding Package", Description = "Elegant wedding reception for up to 120 guests.", PricePerPerson = 65m, MinGuests = 60, IncludedServices = "4-course plated menu, floral centerpieces, dedicated service team, cake service, DJ booth", ImageUrl = "/assets/img/event1.svg" },
            new() { Name = "Corporate Boardroom Lunch", Description = "Business lunches with premium catering service.", PricePerPerson = 38m, MinGuests = 10, IncludedServices = "3-course executive menu, projector & screen, unlimited coffee, dedicated waiter", ImageUrl = "/assets/img/event2.svg" },
            new() { Name = "Birthday Celebration", Description = "Fun, festive and fully customizable.", PricePerPerson = 28m, MinGuests = 8, IncludedServices = "2-course menu, decoration theme, birthday cake, party host", ImageUrl = "/assets/img/event3.svg" }
        );
        await db.SaveChangesAsync(); // packages need ids before bookings
        db.EventBookings.AddRange(
            new() { CustomerName = "Nina & Alex", Phone = "+1-555-8001", Email = "nina.alex@mail.com", Type = EventType.Wedding, PackageId = 1, EventDate = DateTime.UtcNow.AddDays(21), GuestCount = 100, Venue = EventVenue.OurHall, QuoteAmount = 6500m, DepositAmount = 1000m, DepositPaid = true, Status = EventStatus.Confirmed, MenuNotes = "Vegetarian options for 8 guests", Tasks = { new() { Title = "Confirm floral arrangement", DueDate = DateTime.UtcNow.AddDays(7), AssignedToId = 4 } } },
            new() { CustomerName = "TechNova Inc.", Phone = "+1-555-8002", Type = EventType.Corporate, PackageId = 2, EventDate = DateTime.UtcNow.AddDays(5), GuestCount = 24, Venue = EventVenue.Terrace, QuoteAmount = 912m, Status = EventStatus.Quoted },
            new() { CustomerName = "Lily Chen", Phone = "+1-555-8003", Type = EventType.Birthday, PackageId = 3, EventDate = DateTime.UtcNow.AddDays(-4), GuestCount = 30, Venue = EventVenue.OurHall, QuoteAmount = 840m, DepositPaid = true, Status = EventStatus.Completed }
        );
        await db.SaveChangesAsync();

        // ---------- Coupons ----------
        db.Coupons.AddRange(
            new() { Code = "WELCOME15", Description = "15% off your first online order", Type = CouponType.Percent, Value = 15, MinOrderAmount = 20, MaxUses = 500, ValidTo = DateTime.UtcNow.AddMonths(3) },
            new() { Code = "FREESHIP", Description = "Free delivery", Type = CouponType.Fixed, Value = 3.50m, MinOrderAmount = 15, MaxUses = 300, ValidTo = DateTime.UtcNow.AddMonths(1) },
            new() { Code = "TASTEOF10", Description = "$10 off orders above $60", Type = CouponType.Fixed, Value = 10, MinOrderAmount = 60, MaxUses = 100, ValidTo = DateTime.UtcNow.AddMonths(2) }
        );

        // ---------- Historic orders (30 days) for analytics ----------
        var orderRandom = new Random(7);
        var methodPool = new[] { PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Card, PaymentMethod.Online, PaymentMethod.QrCode };
        var typePool = new[] { OrderType.DineIn, OrderType.DineIn, OrderType.DineIn, OrderType.Delivery, OrderType.Pickup, OrderType.Takeaway };
        var waiterIds = new[] { 4, 5 };
        long counter = 1;
        for (int d = 30; d >= 1; d--)
        {
            int ordersThatDay = orderRandom.Next(9, 17);
            for (int k = 0; k < ordersThatDay; k++)
            {
                var created = DateTime.UtcNow.AddDays(-d).AddHours(orderRandom.Next(11, 22)).AddMinutes(orderRandom.Next(0, 59));
                var type = typePool[orderRandom.Next(typePool.Length)];
                var dishCount = orderRandom.Next(1, 4);
                decimal subtotal = 0; decimal cost = 0;
                var order = new Order
                {
                    OrderNumber = $"ORD-{created:yyyy}-{counter:D5}",
                    Type = type,
                    Status = OrderStatus.Completed,
                    PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = created,
                    CreatedById = type == OrderType.DineIn ? waiterIds[orderRandom.Next(2)] : 3,
                    GuestName = type == OrderType.DineIn ? $"Walk-in guest #{counter}" : null,
                    TableId = type == OrderType.DineIn ? orderRandom.Next(1, 13) : null,
                    UpdatedAt = created.AddHours(1),
                };
                for (int j = 0; j < dishCount; j++)
                {
                    var mi = items[orderRandom.Next(items.Count)];
                    var qty = orderRandom.Next(1, 3);
                    order.Items.Add(new OrderItem { MenuItemId = mi.Id, NameSnapshot = mi.Name, UnitPrice = mi.Price, Quantity = qty, LineTotal = mi.Price * qty, Status = LineItemStatus.Served, CostSnapshot = mi.Cost * qty });
                    subtotal += mi.Price * qty; cost += mi.Cost * qty;
                }
                var tax = Math.Round(subtotal * 0.09m, 2);
                var service = type == OrderType.DineIn ? Math.Round(subtotal * 0.05m, 2) : 0;
                var delivery = type == OrderType.Delivery ? 3.5m : 0;
                order.Subtotal = subtotal; order.TaxAmount = tax; order.ServiceCharge = service; order.DeliveryFee = delivery;
                order.Total = subtotal + tax + service + delivery;
                db.Orders.Add(order);
                db.Payments.Add(new Payment { Order = order, Method = methodPool[orderRandom.Next(methodPool.Length)], Amount = order.Total, PaidAt = created.AddHours(1), CashierId = 3, TransactionRef = $"TXN-{counter:D6}" });
                counter++;
            }
        }

        // ---------- Active live orders (kitchen demo) ----------
        var live1 = new Order { OrderNumber = $"ORD-{DateTime.UtcNow:yyyy}-{counter + 1:D5}", Type = OrderType.DineIn, Status = OrderStatus.Preparing, PaymentStatus = PaymentStatus.Unpaid, TableId = 2, CreatedById = 4, CreatedAt = DateTime.UtcNow.AddMinutes(-18), GuestName = "Walk-in guest", Subtotal = 58, TaxAmount = 5.22m, ServiceCharge = 2.9m, Total = 66.12m };
        live1.Items.Add(new OrderItem { MenuItemId = 4, Order = live1, NameSnapshot = "Wagyu Ribeye 300g", UnitPrice = 58m, Quantity = 1, LineTotal = 58m, Status = LineItemStatus.Preparing, ModifierText = "Medium Rare", CostSnapshot = 24m });
        var live2 = new Order { OrderNumber = $"ORD-{DateTime.UtcNow:yyyy}-{counter + 2:D5}", Type = OrderType.Delivery, Status = OrderStatus.Confirmed, PaymentStatus = PaymentStatus.Paid, CreatedById = 3, CreatedAt = DateTime.UtcNow.AddMinutes(-7), GuestName = "Sofia Rossi", Phone = "+1-555-7003", DeliveryAddress = "88 Maple Street, Apt 4B", DeliveryFee = 3.5m, CouponCode = "WELCOME15", Subtotal = 32.4m, DiscountAmount = 4.86m, TaxAmount = 2.92m, Total = 33.96m };
        live2.Items.Add(new OrderItem { MenuItemId = 9, Order = live2, NameSnapshot = "Truffle Funghi Pizza", UnitPrice = 19.5m, Quantity = 1, LineTotal = 19.5m, Status = LineItemStatus.Pending, CostSnapshot = 5.6m });
        live2.Items.Add(new OrderItem { MenuItemId = 11, Order = live2, NameSnapshot = "Molten Lava Cake", UnitPrice = 9.9m, Quantity = 1, LineTotal = 9.9m, Status = LineItemStatus.Pending, CostSnapshot = 2.4m });
        live2.Items.Add(new OrderItem { MenuItemId = 13, Order = live2, NameSnapshot = "Signature Aurora Mocktail", UnitPrice = 7.5m, Quantity = 1, LineTotal = 7.5m, Status = LineItemStatus.Pending, ModifierText = "No Ice", CostSnapshot = 1.6m });
        db.Orders.AddRange(live1, live2);
        db.Payments.Add(new Payment { Order = live2, Method = PaymentMethod.Online, Amount = 33.96m, PaidAt = DateTime.UtcNow.AddMinutes(-7), TransactionRef = "TXN-LIVE-002" });

        // ---------- Expenses (last 2 months) ----------
        var month = DateTime.UtcNow.AddDays(-30);
        db.Expenses.AddRange(
            new() { Category = ExpenseCategory.Rent, Amount = 4800m, Description = "Monthly rent — main building", Date = month, PaidTo = "Downtown Properties", CreatedById = 8, ReceiptRef = "RC-1001" },
            new() { Category = ExpenseCategory.Utilities, Amount = 740m, Description = "Electricity & water", Date = month, PaidTo = "CityUtilities", CreatedById = 8 },
            new() { Category = ExpenseCategory.Marketing, Amount = 620m, Description = "Instagram ads + food bloggers", Date = month.AddDays(5), PaidTo = "Meta Ads", CreatedById = 8 },
            new() { Category = ExpenseCategory.Supplies, Amount = 380m, Description = "Takeaway packaging", Date = month.AddDays(12), PaidTo = "EcoPack Ltd", CreatedById = 8 },
            new() { Category = ExpenseCategory.Rent, Amount = 4800m, Description = "Monthly rent — main building", Date = DateTime.UtcNow, PaidTo = "Downtown Properties", CreatedById = 8, ReceiptRef = "RC-2001" },
            new() { Category = ExpenseCategory.Maintenance, Amount = 260m, Description = "Kitchen hood deep clean", Date = DateTime.UtcNow.AddDays(-3), PaidTo = "CleanPro", CreatedById = 8 }
        );

        // ---------- Payroll ----------
        var period = DateTime.UtcNow.ToString("yyyy-MM");
        foreach (var u in users.Where(u => u.BaseSalary.HasValue && u.Role != AppRole.Customer))
            db.Payrolls.Add(new Payroll { EmployeeId = u.Id, Period = period, BaseSalary = u.BaseSalary!.Value, NetPay = u.BaseSalary.Value, Status = PayrollStatus.Pending });

        // ---------- Reviews ----------
        db.Reviews.AddRange(
            new() { CustomerName = "Emily Carter", Rating = 5, Comment = "The wagyu was unreal. Service was flawless — Liam remembered our anniversary!", Type = ReviewType.Overall, Status = ReviewStatus.Approved },
            new() { CustomerName = "James Wu", Rating = 4, Comment = "Great pizza, slightly slow on a busy Friday. Will come again.", Type = ReviewType.Food, Status = ReviewStatus.Approved, Reply = "Thank you James — we have added a second pizza station since!" },
            new() { CustomerName = "Omar Hassan", Rating = 5, Comment = "Delivery arrived hot and 10 minutes early.", Type = ReviewType.Delivery, Status = ReviewStatus.Approved },
            new() { CustomerName = "Anonymous", Rating = 3, Comment = "Dessert was sold out when I visited.", Type = ReviewType.Food, Status = ReviewStatus.Pending }
        );

        // ---------- Notifications ----------
        db.Notifications.AddRange(
            new() { Title = "Low stock alert", Message = "Atlantic Salmon Fillet (9.2 kg) is below minimum (10 kg).", Type = NotificationType.LowStock, TargetRole = AppRole.Manager, Link = "/admin#inventory" },
            new() { Title = "New event inquiry", Message = "TechNova Inc. requested a quote for Corporate Boardroom Lunch — 24 guests.", Type = NotificationType.EventInquiry, TargetRole = AppRole.Manager, Link = "/admin#events" },
            new() { Title = "New review pending", Message = "A 3-star review is waiting for moderation.", Type = NotificationType.Review, TargetRole = AppRole.Manager, Link = "/admin#reviews" },
            new() { Title = "Kitchen busy", Message = "1 ticket older than 15 minutes on the pass.", Type = NotificationType.NewOrder, TargetRole = AppRole.Chef, Link = "/kitchen" }
        );

        // ---------- Shifts & attendance ----------
        var today = DateTime.UtcNow.Date;
        db.Shifts.AddRange(
            new() { EmployeeId = 3, StartTime = today.AddHours(9), EndTime = today.AddHours(17), Status = ShiftStatus.Completed },
            new() { EmployeeId = 4, StartTime = today.AddHours(16), EndTime = today.AddHours(24), Status = ShiftStatus.InProgress },
            new() { EmployeeId = 6, StartTime = today.AddHours(14), EndTime = today.AddHours(23), Status = ShiftStatus.InProgress },
            new() { EmployeeId = 5, StartTime = today.AddDays(1).AddHours(10), EndTime = today.AddDays(1).AddHours(18), Status = ShiftStatus.Scheduled }
        );
        db.Attendances.AddRange(
            new() { EmployeeId = 3, CheckIn = today.AddHours(8.9), CheckOut = today.AddHours(17.1) },
            new() { EmployeeId = 4, CheckIn = today.AddHours(15.95) },
            new() { EmployeeId = 6, CheckIn = today.AddHours(13.9) }
        );

        await db.SaveChangesAsync();
    }
}
