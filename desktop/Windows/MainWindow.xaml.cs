using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Views;

namespace SmartRestaurant.Desktop.Windows;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clock = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, UserControl> _viewCache = new();

    public MainWindow()
    {
        InitializeComponent();
        UserNameText.Text = Session.DisplayName;
        RoleText.Text = Session.Role + (Session.BranchId != null ? " • branch" : " • all branches");
        BranchText.Text = "all branches";
        BuildNav();
        BuildBranchSelector();
        App.Realtime.NotificationReceived += OnNotification;
        App.Realtime.ConnectionChanged += OnConnection;
        App.Realtime.StatsRefresh += _ => { };
        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        _clock.Start();
        Navigate("Overview");
    }

    // ───────────── role-based navigation ─────────────
    private void BuildNav()
    {
        NavPanel.Children.Clear();
        void Add(string key, string icon, string label, bool visible)
        {
            if (!visible) return;
            var rb = new RadioButton { GroupName = "nav", Tag = key, Style = (Style)FindResource("NavBtn"), IsChecked = key == "Overview" };
            rb.Content = new TextBlock { Text = icon + "   " + label, FontSize = 13.5 };
            rb.Checked += (_, _) => Navigate(key);
            NavPanel.Children.Add(rb);
        }

        var role = Session.Role;
        bool adminish = role is "SuperAdmin" or "Manager";
        Add("Overview", "📊", "Overview", true);
        Add("Orders", "🧾", "Orders", role != "Kitchen");
        Add("KDS", "👨‍🍳", "Kitchen Display", role is "Kitchen" or "SuperAdmin" or "Manager");
        Add("Tables", "🪑", "Tables & Floor", role is "Waiter" or "SuperAdmin" or "Manager" or "Cashier");
        Add("Reservations", "📅", "Reservations", adminish || role == "Waiter");
        Add("Menu", "🍽", "Menu & Recipes", adminish);
        Add("Inventory", "📦", "Inventory", adminish || role == "Inventory");
        Add("Payments", "💳", "Payments", adminish || role == "Cashier");
        Add("Accounting", "📈", "Accounting", adminish || role == "Accountant");
        Add("Cameras", "📹", "Cameras", adminish);
        Add("Events", "🎉", "Events", adminish);
        Add("Branches", "🏢", "Branches", role == "SuperAdmin");
        Add("Users", "👥", "Users & Roles", adminish);
        Add("Settings", "⚙", "Settings", role == "SuperAdmin");
    }

    private void BuildBranchSelector()
    {
        if (!Session.IsSuperAdmin && Session.Role != "Manager") return;
        _ = Task.Run(async () =>
        {
            try
            {
                var branches = await App.Api.GetAsync<List<BranchDto>>("/api/v1/branches");
                Dispatcher.BeginInvoke(() =>
                {
                    BranchSelector.ItemsSource = branches;
                    BranchSelector.DisplayMemberPath = "Name";
                    BranchSelector.SelectedIndex = 0;
                    BranchSelector.Visibility = Visibility.Visible;
                });
            }
            catch { }
        });
    }

    private Guid? SelectedBranchId => Session.IsSuperAdmin
        ? (BranchSelector.SelectedItem as BranchDto)?.Id
        : Session.BranchId;

    public static Guid? CurrentBranchId { get; private set; }

    private void BranchSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (BranchSelector.SelectedItem is BranchDto b)
        {
            BranchText.Text = b.Name;
            CurrentBranchId = b.Id;
            ReloadCurrentView();
        }
    }

    // ───────────── navigation ─────────────
    private void Navigate(string key)
    {
        PageTitle.Text = key switch
        {
            "Overview" => "Overview",
            "Orders" => "Orders & Billing",
            "KDS" => "Kitchen Display System",
            "Tables" => "Tables & Floor Plan",
            "Reservations" => "Reservations",
            "Menu" => "Menu & Recipes",
            "Inventory" => "Inventory — Auto-deduction Engine",
            "Payments" => "Payments — Iranian Gateways",
            "Accounting" => "Accounting",
            "Cameras" => "Live Cameras",
            "Events" => "Events & Promotions",
            "Branches" => "Branches",
            "Users" => "Users & Roles",
            "Settings" => "Settings & Gateways",
            _ => key
        };
        CurrentBranchId = SelectedBranchId ?? Session.BranchId;
        try
        {
            if (!_viewCache.TryGetValue(key, out var view))
            {
                view = key switch
                {
                    "Overview" => new OverviewView(),
                    "Orders" => new OrdersView(),
                    "KDS" => new KdsView(),
                    "Tables" => new TablesView(),
                    "Reservations" => new ReservationsView(),
                    "Menu" => new MenuView(),
                    "Inventory" => new InventoryView(),
                    "Payments" => new PaymentsView(),
                    "Accounting" => new AccountingView(),
                    "Cameras" => new CamerasView(),
                    "Events" => new EventsView(),
                    "Branches" => new BranchesView(),
                    "Users" => new UsersView(),
                    "Settings" => new SettingsView(),
                    _ => new OverviewView()
                };
                _viewCache[key] = view;
            }
            ContentHost.Content = view;
            if (view is IRefreshable r) _ = r.RefreshAsync();
        }
        catch (Exception ex)
        {
            Toast("❌ " + ex.Message);
        }
    }

    private void ReloadCurrentView()
    {
        var keys = _viewCache.Keys.ToList();
        _viewCache.Clear();
        if (PageTitle.Text == "Overview") { Navigate("Overview"); return; }
        var current = ContentHost.Content as UserControl;
        if (current != null)
        {
            // re-navigate to current page title
            var title = PageTitle.Text;
            var match = keys.FirstOrDefault(k => title.Contains(k, StringComparison.OrdinalIgnoreCase)) ?? "Overview";
            Navigate(match);
        }
    }

    // ───────────── notifications ─────────────
    private void OnNotification(NotificationDto n)
    {
        if (!n.IsRead) App.Audio.Chime();
        Toast($"{IconFor(n.Type)}  {n.Title}");
        if (BellPopup.Visibility == Visibility.Visible) _ = LoadBellAsync();
    }

    private static string IconFor(string type) => type switch
    {
        "NewOrder" => "🧾", "OrderReady" => "🔔", "LowStock" => "📦",
        "Payment" => "💳", "Reservation" => "📅", _ => "ℹ️"
    };

    private void OnConnection(string state)
    {
        LiveDot.Fill = new SolidColorBrush(Color.FromRgb(
            (byte)(state == "online" ? 0x4A : 0xF8), (byte)(state == "online" ? 0xDE : 0x87), (byte)(state == "online" ? 0x80 : 0x71)));
        LiveText.Text = state == "online" ? "live" : "offline";
    }

    private async Task LoadBellAsync()
    {
        try
        {
            var list = await App.Api.GetAsync<List<NotificationDto>>("/api/v1/notifications");
            BellList.Children.Clear();
            foreach (var n in list.Take(30))
            {
                var b = new Border
                {
                    Background = new SolidColorBrush(n.IsRead ? Color.FromArgb(0, 0, 0, 0) : Color.FromRgb(0x1B, 0x23, 0x38)),
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand
                };
                b.Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = IconFor(n.Type) + "  " + n.Title, FontWeight = FontWeights.SemiBold, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = n.Message, Foreground = (Brush)FindResource("Muted"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) },
                        new TextBlock { Text = n.CreatedAt.ToLocalTime().ToString("MMM dd HH:mm"), Foreground = (Brush)FindResource("Muted"), FontSize = 10, Margin = new Thickness(0, 3, 0, 0) }
                    }
                };
                b.MouseDown += async (_, _) => { await App.Api.PutAsync<object>($"/api/v1/notifications/{n.Id}/read", new { }); };
                BellList.Children.Add(b);
            }
        }
        catch { }
    }

    private void Bell_Click(object sender, RoutedEventArgs e)
    {
        BellPopup.Visibility = BellPopup.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (BellPopup.Visibility == Visibility.Visible) _ = LoadBellAsync();
    }

    private void BellPopup_Backdrop(object sender, MouseButtonEventArgs e) => BellPopup.Visibility = Visibility.Collapsed;

    private async void MarkAll_Click(object sender, RoutedEventArgs e)
    {
        try { await App.Api.PutAsync<object>("/api/v1/notifications/read-all", new { }); await LoadBellAsync(); } catch { }
    }

    // ───────────── toasts ─────────────
    public void Toast(string message)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x23, 0x38)),
            BorderBrush = (Brush)FindResource("Accent"),
            BorderThickness = new Thickness(0, 0, 0, 2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 11, 16, 11),
            Margin = new Thickness(0, 0, 0, 8),
            MinWidth = 280,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 3, Opacity = 0.45 },
            Child = new TextBlock { Text = message, FontSize = 12.5, MaxWidth = 360, TextWrapping = TextWrapping.Wrap }
        };
        ToastHost.Children.Insert(0, border);
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        t.Tick += (_, _) => { border.Opacity = 0; t.Stop(); };
        t.Start();
        var remove = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        remove.Tick += (_, _) => { ToastHost.Children.Remove(border); remove.Stop(); };
        remove.Start();
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        Session.Clear();
        _ = App.Realtime.DisconnectAsync();
        new LoginWindow().Show();
        Close();
    }
}

public interface IRefreshable { Task RefreshAsync(); }
