using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Views;

/// <summary>Kitchen Display System — push tickets (SignalR), station filter, live timers, bump bars, chime.</summary>
public class KdsView : UserControl, IRefreshable
{
    private readonly WrapPanel _tickets = new WrapPanel() .Margin(0, 14, 0, 0);
    private readonly Dictionary<Guid, KdsTicketDto> _live = new();
    private readonly DispatcherTimer _timer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(5) };
    private readonly ComboBox _station = new ComboBox() .Width(170);
    private readonly TextBlock _count = Ui.Muted("");

    public KdsView()
    {
        var top = Ui.Row(
            Ui.Label("تیکت‌های زنده آشپزخانه", "#EEF1F7", 14, true),
            _count .Margin(12, 2, 0, 0),
            Ui.Label("ایستگاه:", "#9AA7C0", 12) .Margin(24, 2, 8, 0),
            _station,
            new Button { Content = "↻", Margin = new Thickness(10, 0, 0, 0) });
        ((Button)top.Children[^1]).Click += async (_, _) => await RefreshAsync();
        _station.ItemsSource = new List<string> { "همه ایستگاه‌ها", "🥩 کباب", "🍲 آشپزخانه گرم", "🥗 سرد", "🍰 دسر", "🥤 بار" };
        _station.SelectedIndex = 0;
        _station.SelectionChanged += async (_, _) => await RefreshAsync();

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroller.Content = _tickets;
        root.Children.Add(scroller);
        Content = root;

        _timer.Tick += (_, _) => RepaintTimers();
        _timer.Start();

        App.Realtime.TicketCreated += t => Dispatcher.BeginInvoke(() => { _live[t.OrderItemId] = t; Repaint(); if (App.Settings.KdsSound) App.Audio.Chime(); (Application.Current.MainWindow as MainWindow)?.Toast($"🆕 آشپزخانه: {Fa.Num(t.Quantity)}× {t.ItemName} — #{t.OrderNumber}"); });
        App.Realtime.TicketUpdated += t => Dispatcher.BeginInvoke(() => { _live[t.OrderItemId] = t; Repaint(); });
        App.Realtime.TicketRemoved += id => Dispatcher.BeginInvoke(() => { _live.Remove(id); Repaint(); });
        App.Realtime.OrderStatusChanged += (_, num, st) => Dispatcher.BeginInvoke(() => (Application.Current.MainWindow as MainWindow)?.Toast($"#{num} → {st}"));
        Loaded += async (_, _) => await RefreshAsync();
    }

    private static int StationIdx() => 0;

    public async Task RefreshAsync()
    {
        try
        {
            var branchQ = MainWindow.CurrentBranchId != null ? $"&branchId={MainWindow.CurrentBranchId}" : "";
            var tickets = await App.Api.GetAsync<List<KdsTicketDto>>("/api/v1/kds/tickets?x=1" + branchQ);
            _live.Clear();
            foreach (var t in tickets) _live[t.OrderItemId] = t;
            Repaint();
        }
        catch (Exception ex) { _tickets.Children.Clear(); _tickets.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }

    private void Repaint()
    {
        var stationFilter = _station.SelectedIndex; // 0 all, 1 grill, 2 hot, 3 cold, 4 dessert, 5 bar
        var visible = _live.Values
            .Where(t => stationFilter == 0 || (int)t.Station == stationFilter)
            .Where(t => t.Status <= 3)
            .OrderBy(t => t.CreatedAt)
            .ToList();
        _tickets.Children.Clear();
        _count.Text = $"({Fa.Num(visible.Count)} تیکت در صف)";
        foreach (var t in visible) _tickets.Children.Add(TicketCard(t));
        if (visible.Count == 0) _tickets.Children.Add(Ui.Placeholder("صف خالی است! تیکت‌های جدید بلافاصله از طریق SignalR اینجا ظاهر می‌شوند. 🔔"));
    }

    private UIElement TicketCard(KdsTicketDto t)
    {
        var border = Ui.Card();
        var statusText = t.Status switch { 1 => "در صف", 2 => "در حال پخت", 3 => "آماده", _ => "تحویل شد" };
        var statusBg = t.Status switch { 1 => "#2A2438", 2 => "#3A2F14", 3 => "#15301F", _ => "#222" };
        var statusFg = t.Status switch { 1 => "#A78BFA", 2 => "#F4B942", 3 => "#4ADE80", _ => "#888" };

        var late = t.ElapsedMinutes >= 15;
        border.BorderBrush = Ui.Brush(late ? "#F87171" : t.Status == 2 ? "#F4B942" : "#2A3550");
        border.BorderThickness = new Thickness(2);

        var actions = Ui.Row();
        if (t.Status == 1)
        {
            var b = new Button { Content = "🍳 شروع پخت", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
            b.Click += async (_, _) => { await Bump(t, 2); };
            actions.Children.Add(b);
        }
        if (t.Status is 1 or 2)
        {
            var b = new Button { Content = "✅ آماده شد", Margin = new Thickness(8, 0, 0, 0) };
            b.Click += async (_, _) => { await Bump(t, 3); };
            actions.Children.Add(b);
        }
        if (t.Status == 3)
        {
            var b = new Button { Content = "🛎 تحویل داده شد", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
            b.Click += async (_, _) => { await Bump(t, 4); };
            actions.Children.Add(b);
        }

        var elapsedText = $"⏱ {Fa.Duration(t.ElapsedMinutes)}";
        border.Child = Ui.Column(
            Ui.Row(
                Ui.Label($"#{t.OrderNumber}", "#F4B942", 13, true),
                Ui.Badge(t.TableNumber != null ? "میز " + Fa.DigitsToFa(t.TableNumber) : Fa.TypeName(t.TypeName), "#22304A", "#EEF1F7") .Margin(8, 0, 0, 0),
                Ui.Label(elapsedText, late ? "#F87171" : "#9AA7C0", 12, late) .HAlign(System.Windows.HorizontalAlignment.Right)),
            Ui.Row(
                Ui.Label($"{Fa.Num(t.Quantity)}×", "#4ADE80", 21, true) .VAlign(VerticalAlignment.Center).Margin(0, 6, 10, 0),
                Ui.Label(t.ItemName, "#EEF1F7", 16, true) .VAlign(VerticalAlignment.Center).TextWrapping(TextWrapping.Wrap)),
            t.Notes != null ? Ui.Label("📝 " + t.Notes, "#F4B942", 12) .Margin(0, 4, 0, 0) : new TextBlock(),
            Ui.Row(
                Ui.Badge(statusText + " • " + Fa.Station(t.Station.ToString()), statusBg, statusFg),
                actions .HAlign(System.Windows.HorizontalAlignment.Right)) .Margin(0, 10, 0, 0));
        border.Width = 300;
        border.Margin = new Thickness(0, 0, 14, 14);
        border.VerticalAlignment = VerticalAlignment.Top;
        return border;
    }

    private void RepaintTimers()
    {
        bool changed = false;
        foreach (var t in _live.Values)
        {
            var mins = (int)(DateTime.UtcNow - t.CreatedAt).TotalMinutes;
            if (mins != t.ElapsedMinutes) { t.ElapsedMinutes = mins; changed = true; }
        }
        if (changed) Repaint();
    }

    private async Task Bump(KdsTicketDto t, int status)
    {
        try
        {
            var o = await App.Api.PutAsync<OrderDto>($"/api/v1/orders/items/{t.OrderItemId}/status", new { status });
            _live[t.OrderItemId].Status = status;
            Repaint();
        }
        catch (Exception ex) { (Application.Current.MainWindow as MainWindow)?.Toast("⚠ " + ex.Message); }
    }
}
