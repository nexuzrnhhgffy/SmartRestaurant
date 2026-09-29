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
        var addBtn = new Button { Content = "＋ New user", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("Create user", new[] { "Full name", "Username", "Password", "Role (SuperAdmin/Manager/Cashier/Waiter/Kitchen/Accountant/Inventory/Customer)" },
                async vals =>
                {
                    var role = vals[3].ToLowerInvariant() switch
                    {
                        "superadmin" => 1, "manager" => 2, "cashier" => 3, "waiter" => 4,
                        "kitchen" => 5, "accountant" => 6, "customer" => 7, "inventory" => 8,
                        _ => throw new ApiException("Unknown role: " + vals[3])
                    };
                    await App.Api.PostAsync<object>("/api/v1/users", new
                    { fullName = vals[0], userName = vals[1], password = vals[2], role, email = vals[1] + "@zafaran.ir", isActive = true, branchId = MainWindow.CurrentBranchId });
                    (Application.Current.MainWindow as MainWindow)?.Toast("👤 User created");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("Staff accounts & role assignment", "#EEF1F7", 14, true), addBtn);
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
                var chip = Ui.Badge(u.RoleName, "#22304A", roleColors.GetValueOrDefault(u.RoleName, "#EEF1F7"));
                var row = Ui.Row(
                    Ui.Label(u.FullName, "#EEF1F7", 13, true) .Width(170).TextTrimming(TextTrimming.CharacterEllipsis),
                    Ui.Muted("@" + u.UserName) .Width(110),
                    chip .VAlign(VerticalAlignment.Center),
                    Ui.Muted(u.BranchName ?? "all branches") .Margin(12, 0, 0, 0).Width(150),
                    Ui.Label(u.IsActive ? "● active" : "○ disabled", u.IsActive ? "#4ADE80" : "#5B6B8C", 11.5) .HAlign(System.Windows.HorizontalAlignment.Right));
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
        var addBtn = new Button { Content = "＋ New branch", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("Create branch", new[] { "Name", "City", "Address", "Phone" },
                async vals =>
                {
                    await App.Api.PostAsync<object>("/api/v1/branches", new { name = vals[0], city = vals[1], address = vals[2], phone = vals[3], isActive = true });
                    (Application.Current.MainWindow as MainWindow)?.Toast("🏢 Branch created");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("All branches — multi-branch operations", "#EEF1F7", 14, true), addBtn);
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
                        Ui.Badge(b.IsActive ? "ACTIVE" : "OFF", b.IsActive ? "#15301F" : "#222", b.IsActive ? "#4ADE80" : "#888") .HAlign(System.Windows.HorizontalAlignment.Right)),
                    Ui.Muted((b.City ?? "") + " • " + (b.Phone ?? "")) .Margin(0, 8, 0, 0),
                    Ui.Row(
                        Ui.Column(Ui.Muted("30-day revenue"), Ui.Label(Ui.Money(sale?.Revenue ?? 0), "#4ADE80", 15, true)),
                        Ui.Column(Ui.Muted("Orders") .Margin(18, 0, 0, 0), Ui.Label((sale?.Orders ?? 0).ToString(), "#F4B942", 15, true) .Margin(18, 0, 0, 0)))
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
        root.Children.Add(Ui.Label("Global settings — restaurant name, VAT, receipt footer, gateway keys", "#EEF1F7", 14, true));
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
                var save = new Button { Content = "Save", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
                var key = s.Key;
                save.Click += async (_, _) =>
                {
                    try { await App.Api.PutAsync<object>("/api/v1/settings", new { key, value = edit.Text }); (Application.Current.MainWindow as MainWindow)?.Toast("💾 " + key + " saved"); }
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
        var addBtn = new Button { Content = "＋ New event", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("Create event", new[] { "Title", "Emoji banner", "Discount %", "Duration (days from now)" },
                async vals =>
                {
                    var days = double.Parse(vals[3]);
                    await App.Api.PostAsync<object>("/api/v1/events", new
                    { title = vals[0], bannerEmoji = vals[1], discountPercent = decimal.Parse(vals[2]), startAt = DateTime.UtcNow, endAt = DateTime.UtcNow.AddDays(days), isActive = true });
                    (Application.Current.MainWindow as MainWindow)?.Toast("🎉 Event created");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("Promotions — discounts auto-apply to new orders", "#EEF1F7", 14, true), addBtn);
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
                    Ui.Badge(e.IsRunning ? "RUNNING −" + e.DiscountPercent.ToString("0") + "%" : "scheduled", e.IsRunning ? "#3A2F14" : "#22304A", e.IsRunning ? "#F4B942" : "#9AA7C0") .Margin(10, 0, 0, 0),
                    Ui.Muted($"  {e.StartAt.ToLocalTime():MMM d} → {e.EndAt.ToLocalTime():MMM d}") .HAlign(System.Windows.HorizontalAlignment.Right)));
                _list.Children.Add(new Separator { Opacity = 0.1, Margin = new Thickness(0, 10, 0, 10) });
            }
            if (events.Count == 0) _list.Children.Add(Ui.Placeholder("No events yet."));
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
        var addBtn = new Button { Content = "＋ New reservation", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("New reservation", new[] { "Customer name", "Phone", "Party size", "In how many hours?" },
                async vals =>
                {
                    await App.Api.PostAsync<object>("/api/v1/reservations", new
                    { customerName = vals[0], phone = vals[1], partySize = int.Parse(vals[2]), reservedFor = DateTime.UtcNow.AddHours(double.Parse(vals[3])) });
                    (Application.Current.MainWindow as MainWindow)?.Toast("📅 Reservation created");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("Reservation book", "#EEF1F7", 14, true), addBtn);
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
                var status = r.Status switch { 1 => "PENDING", 2 => "CONFIRMED", 3 => "SEATED", 4 => "CANCELLED", _ => "NO-SHOW" };
                var row = Ui.Row(
                    Ui.Label(r.CustomerName, "#EEF1F7", 13, true) .Width(160),
                    Ui.Muted(r.Phone) .Width(120),
                    Ui.Label($"👥 {r.PartySize}", size: 12) .Width(70),
                    Ui.Label(r.ReservedFor.ToLocalTime().ToString("ddd MMM d, HH:mm"), "#9AA7C0", 12),
                    Ui.Badge(status, "#22304A", statusColor) .HAlign(System.Windows.HorizontalAlignment.Right));
                _list.Children.Add(row);
                _list.Children.Add(new Separator { Opacity = 0.1, Margin = new Thickness(0, 10, 0, 10) });
            }
            if (list.Count == 0) _list.Children.Add(Ui.Placeholder("No reservations yet."));
        }
        catch (Exception ex) { _list.Children.Clear(); _list.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}
