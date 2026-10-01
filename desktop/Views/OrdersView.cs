using System.Windows;
using System.Windows.Controls;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Dialogs;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Views;

/// <summary>Orders & billing — status pipeline, payment capture (cash / card / gateway), cancel.</summary>
public class OrdersView : UserControl, IRefreshable
{
    private readonly StackPanel _list = new StackPanel();
    private string _filter = "active";

    public OrdersView()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var filterBar = Ui.Row();
        foreach (var (key, label) in new[] { ("active", "فعال"), ("all", "همه سفارش‌ها"), ("unpaid", "در انتظار پرداخت") })
        {
            var b = new RadioButton { Content = label, GroupName = "of", Tag = key, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 7), IsChecked = key == "active" };
            b.Checked += async (_, _) => { _filter = key; await RefreshAsync(); };
            filterBar.Children.Add(b);
        }
        var refreshBtn = new Button { Content = "↻ بروزرسانی" };
        refreshBtn.Click += async (_, _) => await RefreshAsync();
        filterBar.Children.Add(refreshBtn);

        Grid.SetRow(filterBar, 0);
        var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 14, 0, 0) };
        scroller.Content = _list;
        Grid.SetRow(scroller, 1);
        root.Children.Add(filterBar);
        root.Children.Add(scroller);
        Content = root;
        App.Realtime.StatsRefresh += _ => Dispatcher.BeginInvoke(async () => await RefreshAsync());
        App.Realtime.OrderUpdated += _ => Dispatcher.BeginInvoke(async () => await RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        try
        {
            var branchQ = MainWindow.CurrentBranchId != null ? $"&branchId={MainWindow.CurrentBranchId}" : "";
            var path = _filter switch
            {
                "active" => "/api/v1/orders/active?x=1" + branchQ,
                "unpaid" => $"/api/v1/orders?status=1&x=1{branchQ}",
                _ => "/api/v1/orders?take=40"
            };
            var orders = await App.Api.GetAsync<List<OrderDto>>(path);
            if (_filter == "unpaid") orders = orders.Where(o => o.PaymentStatusName != "Paid" && o.StatusName != "Cancelled").ToList();

            _list.Children.Clear();
            if (orders.Count == 0) { _list.Children.Add(Ui.Placeholder("هنوز سفارشی نیست — از نمای میزها یا سایت آنلاین سفارش بگیرید.")); return; }

            foreach (var o in orders)
                _list.Children.Add(OrderCard(o));
        }
        catch (Exception ex) { _list.Children.Clear(); _list.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }

    private UIElement OrderCard(OrderDto o)
    {
        var statusColor = o.StatusName switch { "Pending" => "#A78BFA", "Confirmed" => "#60A5FA", "Preparing" => "#F4B942", "Ready" => "#4ADE80", "Served" => "#9AA7C0", "Completed" => "#5B6B8C", _ => "#F87171" };
        var paidColor = o.PaymentStatusName == "Paid" ? "#4ADE80" : (o.PaymentStatusName == "Unpaid" ? "#F87171" : "#F4B942");

        var itemsPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var i in o.Items)
        {
            var itemColor = i.Status switch { 2 => "#F4B942", 3 => "#4ADE80", 4 => "#9AA7C0", 5 => "#F87171", _ => "#60A5FA" };
            itemsPanel.Children.Add(Ui.Row(
                Ui.Label($"{Fa.DigitsToFa(i.Quantity.ToString())}× {i.ItemName}", size: 12.5),
                Ui.Label(i.Notes != null ? $"  📝 {i.Notes}" : "", "#F4B942", 11),
                Ui.Label("  " + Fa.Station(i.StationName), "#9AA7C0", 10.5)));
        }

        var actions = Ui.Row();
        void Btn(string label, Func<Task> onClick, bool primary = false)
        {
            var b = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0) };
            if (primary) b.Style = (Style)Application.Current.Resources["PrimaryBtn"];
            b.Click += async (_, _) => { try { b.IsEnabled = false; await onClick(); await RefreshAsync(); } catch (Exception ex) { ToastWin(ex.Message); } finally { b.IsEnabled = true; } };
            actions.Children.Add(b);
        }
        if (o.StatusName == "Pending") Btn("✔ تأیید → ارسال به آشپزخانه", () => App.Api.PutAsync<OrderDto>($"/api/v1/orders/{o.Id}/status", new { status = 2 }), true);
        if (o.StatusName is "Confirmed" or "Preparing") Btn("🍳 در حال آماده‌سازی", () => App.Api.PutAsync<OrderDto>($"/api/v1/orders/{o.Id}/status", new { status = 3 }));
        if (o.StatusName is "Preparing" or "Confirmed") Btn("✅ آماده شد", () => App.Api.PutAsync<OrderDto>($"/api/v1/orders/{o.Id}/status", new { status = 4 }));
        if (o.StatusName == "Ready") Btn("🍽 سرو شد", () => App.Api.PutAsync<OrderDto>($"/api/v1/orders/{o.Id}/status", new { status = 5 }));
        if (o.PaymentStatusName != "Paid" && o.StatusName != "Cancelled" && o.StatusName != "Completed")
            Btn("💳 ثبت پرداخت", async () => { var dlg = new PaymentDialog(o); if (dlg.ShowDialog() == true) await RefreshAsync(); }, true);
        if (o.PaymentStatusName == "Paid" && o.StatusName != "Completed")
            Btn("🏁 تکمیل سفارش (کسر موجودی + سند حسابداری)", () => App.Api.PutAsync<OrderDto>($"/api/v1/orders/{o.Id}/status", new { status = 6 }), true);
        if (o.StatusName is not ("Completed" or "Cancelled")) Btn("✕ لغو", () => App.Api.PutAsync<OrderDto>($"/api/v1/orders/{o.Id}/status", new { status = 7 }));

        var card = Ui.Card(Ui.Column(
            Ui.Row(
                Ui.Label("#" + Fa.DigitsToFa(o.OrderNumber), "#F4B942", 15, true),
                Ui.Badge(Fa.Status(o.StatusName), "#22304A", statusColor) .Margin(10, 0, 0, 0),
                Ui.Badge(Fa.Status(o.PaymentStatusName), "#22304A", paidColor) .Margin(6, 0, 0, 0),
                Ui.Label(Fa.TypeName(o.TypeName) + (o.TableNumber != null ? $" • میز {Fa.Num(o.TableNumber.Value)}" : "") + (o.CustomerName != null ? $" • {o.CustomerName}" : ""), "#9AA7C0", 12) .Margin(10, 0, 0, 0),
                Ui.MoneyEl(o.Total).FontSize(15).FontWeight(FontWeights.Bold).HAlign(System.Windows.HorizontalAlignment.Right)),
            itemsPanel,
            Ui.Row(Ui.Muted($"ثبت‌کننده: {o.CreatedByName ?? "مهمان"} — ساعت {Fa.Time(o.CreatedAt.ToLocalTime())}") .Margin(0, 8, 0, 0)) .HAlign(System.Windows.HorizontalAlignment.Left),
            actions).Margin(0, 12, 0, 0));
        card.Margin = new Thickness(0, 0, 0, 12);
        return card;
    }

    private void ToastWin(string msg) => (Application.Current.MainWindow as MainWindow)?.Toast("⚠ " + msg);
}
