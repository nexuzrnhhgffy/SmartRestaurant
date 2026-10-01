using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Controls;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Views;

/// <summary>Manager/SuperAdmin live command center: KPIs, 7-day revenue, top items, branches, live feed.</summary>
public class OverviewView : UserControl, IRefreshable
{
    private readonly TextBlock _revenue = Ui.Label("—", "#EEF1F7", 24, true);
    private readonly TextBlock _orders = Ui.Label("—", "#EEF1F7", 24, true);
    private readonly TextBlock _avg = Ui.Label("—", "#EEF1F7", 24, true);
    private readonly TextBlock _open = Ui.Label("—", "#EEF1F7", 24, true);
    private readonly TextBlock _stock = Ui.Label("—", "#EEF1F7", 24, true);
    private readonly TextBlock _tables = Ui.Label("—", "#EEF1F7", 24, true);
    private readonly BarChart _chart = new BarChart { Height = 210 };
    private readonly StackPanel _topList = new StackPanel();
    private readonly StackPanel _branchList = new StackPanel();
    private readonly StackPanel _feed = new StackPanel();
    private readonly Border _eventBanner = new Border();

    public OverviewView()
    {
        var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var main = Ui.Column();

        if (Session.IsManagerOrAbove) main.Children.Add(BuildEventBanner());

        var kpis = new UniformGrid { Columns = 6 };
        kpis.Children.Add(Wrap(_revenue, "💰", "درآمد امروز", "#4ADE80"));
        kpis.Children.Add(Wrap(_orders, "🧾", "سفارش امروز", "#F4B942"));
        kpis.Children.Add(Wrap(_avg, "🎟", "میانگین فاکتور", "#60A5FA"));
        kpis.Children.Add(Wrap(_open, "⏱", "سفارش‌های باز", "#A78BFA"));
        kpis.Children.Add(Wrap(_tables, "🪑", "میزها", "#F4B942"));
        kpis.Children.Add(Wrap(_stock, "📦", "موجودی کم", "#F87171"));
        kpis.Margin = new Thickness(0, 0, 0, 16);
        main.Children.Add(kpis);

        var midGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        midGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var chartCard = Ui.Card(Ui.Column(
            Ui.Row(Ui.Label("درآمد — ۷ روز اخیر", null, 14, true)),
            _chart));
        chartCard.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(chartCard, 0);

        var topCard = Ui.Card(Ui.Column(Ui.Label("پرفروش‌ترین‌ها — ۷ روز", null, 14, true), _topList));
        topCard.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(topCard, 1);

        var branchCard = Ui.Card(Ui.Column(Ui.Label("مقایسه شعبه‌ها — ۳۰ روز", null, 14, true), _branchList));
        Grid.SetColumn(branchCard, 2);
        midGrid.Children.Add(chartCard);
        midGrid.Children.Add(topCard);
        midGrid.Children.Add(branchCard);
        main.Children.Add(midGrid);

        main.Children.Add(Ui.Card(Ui.Column(
            Ui.Row(Ui.Label("فعالیت لحظه‌ای", null, 14, true), Ui.Muted("  (پوشش زنده با SignalR)") .VAlign(VerticalAlignment.Bottom)),
            _feed)));
        root.Content = main;
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private static UIElement Wrap(TextBlock value, string icon, string title, string accent)
    {
        var sp = Ui.Column(
            Ui.Row(Ui.Label(icon, size: 18), Ui.Muted(title.ToUpper(), 10) .Margin(7, 3, 0, 0).FontWeight(FontWeights.SemiBold)),
            value);
        var b = Ui.Card(sp, 14);
        b.Margin = new Thickness(0, 0, 10, 0);
        b.BorderBrush = Ui.Brush(accent).Clone();
        b.BorderBrush.Opacity = 0.4;
        return b;
    }

    private Border BuildEventBanner()
    {
        _eventBanner.Background = Ui.Brush("#26202A");
        _eventBorderSetup();
        _eventBanner.Margin = new Thickness(0, 0, 0, 16);
        return _eventBanner;
    }

    private void _eventBorderSetup()
    {
        _eventBanner.CornerRadius = new CornerRadius(14);
        _eventBanner.BorderBrush = Ui.Brush("#F4B942");
        _eventBanner.BorderThickness = new Thickness(1);
        _eventBanner.Padding = new Thickness(16, 10, 16, 10);
    }

    public async Task RefreshAsync()
    {
        try
        {
            var branchQ = MainWindow.CurrentBranchId != null ? $"?branchId={MainWindow.CurrentBranchId}" : "";
            var d = await App.Api.GetAsync<DashboardStatsDto>("/api/v1/reports/dashboard" + branchQ);
            _revenue.Text = Ui.Money(d.TodayRevenue);
            _orders.Text = Fa.Num(d.TodayOrders);
            _avg.Text = Ui.Money(d.AvgTicket);
            _open.Text = Fa.Num(d.OpenOrders);
            _tables.Text = $"{Fa.Num(d.TablesOccupied)}/{Fa.Num(d.TablesTotal)}";
            _stock.Text = Fa.Num(d.LowStockCount);
            _stock.Foreground = d.LowStockCount > 0 ? Ui.Brush("#F87171") : Ui.Brush("#4ADE80");

            _chart.Items = d.Last7Days.Select(x => (Fa.WeekdayShort(x.Date), (double)x.Revenue)).ToList();
            _chart.InvalidateVisual();

            _topList.Children.Clear();
            int rank = 1;
            foreach (var t in d.TopItems.Take(6))
                _topList.Children.Add(Ui.Row(
                    Ui.Label($"{Fa.Num(rank++)}.", "#F4B942", 12, true),
                    Ui.Label(t.Name, size: 12) .Margin(8, 0, 0, 0),
                    new TextBlock { Text = $"×{Fa.Num(t.Qty)}", Foreground = Ui.Brush("#9AA7C0"), FontSize = 11.5, HorizontalAlignment = System.Windows.HorizontalAlignment.Right }
                .Margin(0, 0, 0, 7)));
            _topList.Margin = new Thickness(0, 10, 0, 0);

            _branchList.Children.Clear();
            foreach (var b in d.BranchSales)
            {
                var pct = d.BranchSales.Sum(x => x.Revenue) == 0 ? 0 : (double)(b.Revenue / d.BranchSales.Sum(x => x.Revenue)) * 100;
                _branchList.Children.Add(Ui.Column(
                    Ui.Row(Ui.Label(b.Name, size: 11.5) .MaxWidth(160).TextTrimming(TextTrimming.CharacterEllipsis),
                        Ui.MoneyEl(b.Revenue).FontSize(11.5).HAlign(System.Windows.HorizontalAlignment.Right)),
                    new ProgressBar { Value = pct, Minimum = 0, Maximum = 100, Height = 6, Foreground = Ui.Brush("#F4B942"), Background = Ui.Brush("#1E2740"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 4, 0, 10) }));
            }
            if (d.BranchSales.Count == 0) _branchList.Children.Add(Ui.Muted("حالت تک‌شعبه‌ای — همه فروش‌ها در بالا نمایش داده می‌شود."));

            _feed.Children.Clear();
            if (d.RunningEvent != null)
            {
                _eventBanner.Child = Ui.Row(
                    Ui.Label(d.RunningEvent.BannerEmoji ?? "🎉", size: 22),
                    Ui.Column(
                        Ui.Label($"  {d.RunningEvent.Title} — {Fa.Num((int)d.RunningEvent.DiscountPercent)}٪ تخفیف", "#F4B942", 13.5, true),
                        Ui.Muted("  " + d.RunningEvent.Description ?? "", 11.5) .Margin(12, 0, 0, 0))
                    .Margin(10, 0, 0, 0));
                _eventBanner.Visibility = Visibility.Visible;
            }
            else _eventBanner.Visibility = Visibility.Collapsed;

            var orders = await App.Api.GetAsync<List<OrderDto>>("/api/v1/orders?take=8");
            foreach (var o in orders)
            {
                var color = o.StatusName switch { "Pending" => "#A78BFA", "Confirmed" => "#60A5FA", "Preparing" => "#F4B942", "Ready" => "#4ADE80", "Served" => "#9AA7C0", "Completed" => "#5B6B8C", _ => "#F87171" };
                _feed.Children.Add(Ui.Row(
                    Ui.Label("#" + Fa.DigitsToFa(o.OrderNumber), color, 12.5, true),
                    Ui.Label($"  {Fa.TypeName(o.TypeName)}" + (o.TableNumber != null ? $" میز {Fa.Num(o.TableNumber.Value)}" : ""), size: 12),
                    Ui.Label(Fa.Status(o.StatusName), color, 11.5),
                    Ui.MoneyEl(o.Total).FontSize(12).HAlign(System.Windows.HorizontalAlignment.Right),
                    Ui.Muted("  " + Fa.Time(o.CreatedAt.ToLocalTime())) .HAlign(System.Windows.HorizontalAlignment.Right)));
            }
            _feed.Margin = new Thickness(0, 10, 0, 0);
        }
        catch (Exception ex)
        {
            _feed.Children.Clear();
            _feed.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12));
        }
    }
}
