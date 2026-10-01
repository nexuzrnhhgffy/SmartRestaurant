using System.Windows;
using System.Windows.Controls;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Dialogs;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Views;

/// <summary>Users & roles management.</summary>
public class UsersView : UserControl, IRefreshable
{
    private readonly StackPanel _list = new StackPanel();

    public UsersView()
    {
        var addBtn = new Button { Content = "＋ کاربر جدید", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("ایجاد کاربر", new[] { "نام و نام خانوادگی", "نام کاربری", "رمز عبور", "نقش (SuperAdmin/Manager/Cashier/Waiter/Kitchen/Accountant/Inventory/Customer)" },
                async vals =>
                {
                    var role = vals[3].ToLowerInvariant() switch
                    {
                        "superadmin" => 1, "manager" => 2, "cashier" => 3, "waiter" => 4,
                        "kitchen" => 5, "accountant" => 6, "customer" => 7, "inventory" => 8,
                        _ => throw new ApiException("نقش ناشناخته: " + vals[3])
                    };
                    await App.Api.PostAsync<object>("/api/v1/users", new
                    { fullName = vals[0], userName = vals[1], password = vals[2], role, email = vals[1] + "@zafaran.ir", isActive = true, branchId = MainWindow.CurrentBranchId });
                    (Application.Current.MainWindow as MainWindow)?.Toast("👤 کاربر ایجاد شد");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("حساب‌های کارکنان و تعیین نقش", "#EEF1F7", 14, true), addBtn);
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sc.Content = Ui.Card(Ui.Column(_list)) .Margin(0, 12, 0, 0);
        root.Children.Add(sc);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var users = await App.Api.GetAsync<List<UserDto>>("/api/v1/users");
            _list.Children.Clear();
            var roleColors = new Dictionary<string, string>
            {
                ["SuperAdmin"] = "#F87171", ["Manager"] = "#F4B942", ["Cashier"] = "#4ADE80",
                ["Waiter"] = "#60A5FA", ["Kitchen"] = "#A78BFA", ["Accountant"] = "#34D399",
                ["Customer"] = "#9AA7C0", ["Inventory"] = "#FB923C"
            };
            foreach (var u in users)
            {
                var chip = Ui.Badge(Fa.Role(u.RoleName), "#22304A", roleColors.GetValueOrDefault(u.RoleName, "#EEF1F7"));
                var row = Ui.Row(
                    Ui.Label(u.FullName, "#EEF1F7", 13, true) .Width(170).TextTrimming(TextTrimming.CharacterEllipsis),
                    Ui.Muted("@" + u.UserName) .Width(110),
                    chip .VAlign(VerticalAlignment.Center),
                    Ui.Muted(u.BranchName ?? "همه شعبه‌ها") .Margin(12, 0, 0, 0).Width(150),
                    Ui.Label(u.IsActive ? "● فعال" : "○ غیرفعال", u.IsActive ? "#4ADE80" : "#5B6B8C", 11.5) .HAlign(System.Windows.HorizontalAlignment.Right));
                _list.Children.Add(row);
                _list.Children.Add(new Separator { Opacity = 0.12, Margin = new Thickness(0, 8, 0, 8) });
            }
        }
        catch (Exception ex) { _list.Children.Clear(); _list.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}

/// <summary>Branch management (SuperAdmin).</summary>
public class BranchesView : UserControl, IRefreshable
{
    private readonly WrapPanel _grid = new WrapPanel();

    public BranchesView()
    {
        var addBtn = new Button { Content = "＋ شعبه جدید", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("ایجاد شعبه", new[] { "نام", "شهر", "آدرس", "تلفن" },
                async vals =>
                {
                    await App.Api.PostAsync<object>("/api/v1/branches", new { name = vals[0], city = vals[1], address = vals[2], phone = vals[3], isActive = true });
                    (Application.Current.MainWindow as MainWindow)?.Toast("🏢 شعبه ایجاد شد");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("همه شعبه‌ها — عملیات چندشعبه‌ای", "#EEF1F7", 14, true), addBtn);
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sc.Content = _grid .Margin(0, 12, 0, 0);
        root.Children.Add(sc);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var stats = await App.Api.GetAsync<DashboardStatsDto>("/api/v1/reports/dashboard");
            var branches = await App.Api.GetAsync<List<BranchDto>>("/api/v1/branches");
            _grid.Children.Clear();
            foreach (var b in branches)
            {
                var sale = stats.BranchSales.FirstOrDefault(s => s.BranchId == b.Id);
                var card = Ui.Card();
                card.Width = 300; card.Margin = new Thickness(0, 0, 14, 14);
                card.Child = Ui.Column(
                    Ui.Row(Ui.Label("🏢  " + b.Name, "#EEF1F7", 14, true) .TextWrapping(TextWrapping.Wrap).MaxWidth(200),
                        Ui.Badge(b.IsActive ? "فعال" : "غیرفعال", b.IsActive ? "#15301F" : "#222", b.IsActive ? "#4ADE80" : "#888") .HAlign(System.Windows.HorizontalAlignment.Right)),
                    Ui.Muted((b.City ?? "") + " • " + (b.Phone ?? "")) .Margin(0, 8, 0, 0),
                    Ui.Row(
                        Ui.Column(Ui.Muted("درآمد ۳۰ روز"), Ui.Label(Ui.Money(sale?.Revenue ?? 0), "#4ADE80", 15, true)),
                        Ui.Column(Ui.Muted("سفارش‌ها") .Margin(18, 0, 0, 0), Ui.Label(Fa.Num(sale?.Orders ?? 0), "#F4B942", 15, true) .Margin(18, 0, 0, 0)))
                    .Margin(0, 12, 0, 0));
                _grid.Children.Add(card);
            }
        }
        catch (Exception ex) { _grid.Children.Clear(); _grid.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}

/// <summary>System settings (SuperAdmin): key/value editing incl. gateway keys.</summary>
public class SettingsView : UserControl, IRefreshable
{
    private readonly StackPanel _list = new StackPanel();

    public SettingsView()
    {
        var root = new DockPanel();
        root.Children.Add(Ui.Label("تنظیمات کلی — نام رستوران، مالیات، پانوشت رسید، کلید درگاه‌ها", "#EEF1F7", 14, true));
        DockPanel.SetDock(root.Children[^1], Dock.Top);
        var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0) };
        sc.Content = Ui.Card(Ui.Column(_list));
        root.Children.Add(sc);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var settings = await App.Api.GetAsync<List<SettingDto>>("/api/v1/settings");
            _list.Children.Clear();
            foreach (var s in settings)
            {
                var edit = new TextBox { Text = s.Value, Width = 260, FontSize = 12 };
                var save = new Button { Content = "ذخیره", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
                var key = s.Key;
                save.Click += async (_, _) =>
                {
                    try { await App.Api.PutAsync<object>("/api/v1/settings", new { key, value = edit.Text }); (Application.Current.MainWindow as MainWindow)?.Toast("💾 " + key + " ذخیره شد"); }
                    catch (Exception ex) { (Application.Current.MainWindow as MainWindow)?.Toast("⚠ " + ex.Message); }
                };
                _list.Children.Add(Ui.Row(
                    Ui.Column(
                        Ui.Label(key, "#F4B942", 12, true),
                        Ui.Muted(s.Description ?? "") .Margin(0, 1, 0, 0))
                    .Width(320),
                    edit,
                    save .Margin(10, 0, 0, 0)));
                _list.Children.Add(new Separator { Opacity = 0.1, Margin = new Thickness(0, 12, 0, 12) });
            }
        }
        catch (Exception ex) { _list.Children.Clear(); _list.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}

/// <summary>Events & promotions.</summary>
public class EventsView : UserControl, IRefreshable
{
    private readonly StackPanel _list = new StackPanel();

    public EventsView()
    {
        var addBtn = new Button { Content = "＋ رویداد جدید", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("ایجاد رویداد", new[] { "عنوان", "ایموجی بنر", "درصد تخفیف", "مدت (روز از امروز)" },
                async vals =>
                {
                    var days = double.Parse(vals[3]);
                    await App.Api.PostAsync<object>("/api/v1/events", new
                    { title = vals[0], bannerEmoji = vals[1], discountPercent = decimal.Parse(vals[2]), startAt = DateTime.UtcNow, endAt = DateTime.UtcNow.AddDays(days), isActive = true });
                    (Application.Current.MainWindow as MainWindow)?.Toast("🎉 رویداد ایجاد شد");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("جشنواره‌ها — تخفیف به‌صورت خودکار روی سفارش‌های جدید اعمال می‌شود", "#EEF1F7", 14, true), addBtn);
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var sc = new ScrollViewer();
        sc.Content = Ui.Card(Ui.Column(_list)) .Margin(0, 12, 0, 0);
        root.Children.Add(sc);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var events = await App.Api.GetAsync<List<EventDto>>("/api/v1/events");
            _list.Children.Clear();
            foreach (var e in events)
            {
                _list.Children.Add(Ui.Row(
                    Ui.Label(e.BannerEmoji + "  " + e.Title, "#EEF1F7", 13.5, true),
                    Ui.Badge(e.IsRunning ? "در حال اجرا −" + Fa.Num((int)e.DiscountPercent) + "٪" : "برنامه‌ریزی شده", e.IsRunning ? "#3A2F14" : "#22304A", e.IsRunning ? "#F4B942" : "#9AA7C0") .Margin(10, 0, 0, 0),
                    Ui.Muted($"  {Fa.Jalali(e.StartAt.ToLocalTime())} تا {Fa.Jalali(e.EndAt.ToLocalTime())}") .HAlign(System.Windows.HorizontalAlignment.Right)));
                _list.Children.Add(new Separator { Opacity = 0.1, Margin = new Thickness(0, 10, 0, 10) });
            }
            if (events.Count == 0) _list.Children.Add(Ui.Placeholder("هنوز رویدادی ثبت نشده است."));
        }
        catch (Exception ex) { _list.Children.Clear(); _list.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}

/// <summary>Reservations book.</summary>
public class ReservationsView : UserControl, IRefreshable
{
    private readonly StackPanel _list = new StackPanel();

    public ReservationsView()
    {
        var addBtn = new Button { Content = "＋ رزرو جدید", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("ثبت رزرو", new[] { "نام مشتری", "تلفن", "تعداد نفرات", "چند ساعت دیگر؟" },
                async vals =>
                {
                    await App.Api.PostAsync<object>("/api/v1/reservations", new
                    { customerName = vals[0], phone = vals[1], partySize = int.Parse(vals[2]), reservedFor = DateTime.UtcNow.AddHours(double.Parse(vals[3])) });
                    (Application.Current.MainWindow as MainWindow)?.Toast("📅 رزرو ثبت شد");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("دفتر رزرو میزها", "#EEF1F7", 14, true), addBtn);
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var sc = new ScrollViewer();
        sc.Content = Ui.Card(Ui.Column(_list)) .Margin(0, 12, 0, 0);
        root.Children.Add(sc);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var list = await App.Api.GetAsync<List<ReservationDto>>("/api/v1/reservations");
            _list.Children.Clear();
            foreach (var r in list)
            {
                var statusColor = r.Status switch { 1 => "#F4B942", 2 => "#4ADE80", 3 => "#60A5FA", _ => "#888" };
                var status = r.Status switch { 1 => "در انتظار", 2 => "تأیید شده", 3 => "نشسته", 4 => "لغو شده", _ => "عدم حضور" };
                var row = Ui.Row(
                    Ui.Label(r.CustomerName, "#EEF1F7", 13, true) .Width(160),
                    Ui.Muted(r.Phone) .Width(120),
                    Ui.Label($"👥 {Fa.Num(r.PartySize)}", size: 12) .Width(70),
                    Ui.Label(Fa.JalaliTime(r.ReservedFor.ToLocalTime()), "#9AA7C0", 12),
                    Ui.Badge(status, "#22304A", statusColor) .HAlign(System.Windows.HorizontalAlignment.Right));
                _list.Children.Add(row);
                _list.Children.Add(new Separator { Opacity = 0.1, Margin = new Thickness(0, 10, 0, 10) });
            }
            if (list.Count == 0) _list.Children.Add(Ui.Placeholder("هنوز رزروی ثبت نشده است."));
        }
        catch (Exception ex) { _list.Children.Clear(); _list.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}
