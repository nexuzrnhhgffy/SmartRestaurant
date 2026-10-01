using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Dialogs;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Views;

/// <summary>Table map — status colors, take order (waiter), settle (cashier shortcut), free table.</summary>
public class TablesView : UserControl, IRefreshable
{
    private readonly WrapPanel _grid = new WrapPanel() .Margin(0, 14, 0, 0);
    private readonly DispatcherTimer _timer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(20) };

    public TablesView()
    {
        var top = Ui.Row(Ui.Label("نقشه سالن", "#EEF1F7", 14, true),
            Ui.Badge("🟩 خالی", "#15301F", "#4ADE80") .Margin(14, 0, 0, 0).VAlign(VerticalAlignment.Center),
            Ui.Badge("🟨 اشغال", "#3A2F14", "#F4B942") .Margin(6, 0, 0, 0).VAlign(VerticalAlignment.Center),
            new Button { Content = "↻ بروزرسانی", Margin = new Thickness(14, 0, 0, 0) });
        ((Button)top.Children[^1]).Click += async (_, _) => await RefreshAsync();

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sc.Content = _grid;
        root.Children.Add(sc);
        Content = root;

        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var branchQ = MainWindow.CurrentBranchId != null ? $"&branchId={MainWindow.CurrentBranchId}" : "";
            var tables = await App.Api.GetAsync<List<TableDto>>("/api/v1/tables?x=1" + branchQ);
            _grid.Children.Clear();
            foreach (var t in tables) _grid.Children.Add(TableCard(t));
            if (tables.Count == 0) _grid.Children.Add(Ui.Placeholder("هنوز میزی در این شعبه ثبت نشده است."));
        }
        catch (Exception ex) { _grid.Children.Clear(); _grid.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }

    private UIElement TableCard(TableDto t)
    {
        bool occupied = t.Status == 2;
        var card = Ui.Card();
        card.Width = 168; card.Height = 150;
        card.Margin = new Thickness(0, 0, 14, 14);
        card.Cursor = System.Windows.Input.Cursors.Hand;
        card.BorderBrush = Ui.Brush(occupied ? "#F4B942" : t.Status == 3 ? "#A78BFA" : "#2A3550");
        card.BorderThickness = new Thickness(2);

        card.Child = Ui.Column(
            Ui.Label("🪑", size: 26) .HAlign(System.Windows.HorizontalAlignment.Center),
            Ui.Label("میز " + Fa.Num(t.Number), "#EEF1F7", 16, true) .HAlign(System.Windows.HorizontalAlignment.Center).Margin(0, 4, 0, 0),
            Ui.Muted(Fa.Num(t.Seats) + " نفره") .HAlign(System.Windows.HorizontalAlignment.Center),
            Ui.Label(t.CurrentOrderNumber != null ? "#" + Fa.DigitsToFa(t.CurrentOrderNumber) : Fa.Status(t.StatusName),
                occupied ? "#F4B942" : "#5B6B8C", 11.5, true) .HAlign(System.Windows.HorizontalAlignment.Center).Margin(0, 6, 0, 0),
            occupied && t.OpenAmount != null ? Ui.Label(Ui.Money(t.OpenAmount.Value), "#4ADE80", 12, true) .HAlign(System.Windows.HorizontalAlignment.Center) : new TextBlock());

        card.MouseDown += async (_, _) => await TableClicked(t);
        return card;
    }

    private async Task TableClicked(TableDto t)
    {
        try
        {
            if (t.Status == 2 && t.CurrentOrderId != null)
            {
                var o = await App.Api.GetAsync<OrderDto>($"/api/v1/orders/{t.CurrentOrderId}");
                var dlg = new PaymentDialog(o) { Owner = Application.Current.MainWindow };
                dlg.ShowDialog();
                await RefreshAsync();
            }
            else if (t.Status is 1 or 3)
            {
                var dlg = new TakeOrderDialog(t) { Owner = Application.Current.MainWindow };
                dlg.ShowDialog();
                await RefreshAsync();
            }
        }
        catch (Exception ex) { (Application.Current.MainWindow as MainWindow)?.Toast("⚠ " + ex.Message); }
    }
}

/// <summary>Take-order dialog: pick menu items, quantities, notes → creates a DineIn order.</summary>
public class TakeOrderDialog : Window
{
    private readonly List<MenuItemDto> _menu;
    private readonly Dictionary<Guid, int> _qty = new();
    private readonly Dictionary<Guid, TextBox> _noteBoxes = new();
    private readonly StackPanel _cartPanel = new StackPanel();
    private readonly TableDto _table;
    private decimal _total;

    public TakeOrderDialog(TableDto table)
    {
        _table = table;
        Title = $"ثبت سفارش — میز {Fa.Num(table.Number)}";
        Width = 780; Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Ui.Brush("#0F1420");

        var grid = new Grid { Margin = new Thickness(26, 22, 26, 22) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        FlowDirection = FlowDirection.RightToLeft;

        grid.Children.Add(Ui.Label("منو — برای افزودن کلیک کنید", "#EEF1F7", 15, true));

        var menuScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 10, 14, 0) };
        var wrap = new WrapPanel();
        menuScroll.Content = wrap;
        Grid.SetRow(menuScroll, 1);
        grid.Children.Add(menuScroll);

        var cartCard = Ui.Card();
        Grid.SetColumn(cartCard, 1);
        Grid.SetRow(cartCard, 0);
        Grid.SetRowSpan(cartCard, 2);
        var cartInner = Ui.Column(Ui.Label("🧾 سفارش جاری", "#F4B942", 14, true), _cartPanel .Margin(0, 10, 0, 0));
        cartCard.Child = cartInner;
        grid.Children.Add(cartCard);

        Content = grid;
        _menu = new List<MenuItemDto>();
        _ = LoadMenuAsync(wrap);
    }

    private async Task LoadMenuAsync(WrapPanel wrap)
    {
        try
        {
            var branchQ = MainWindow.CurrentBranchId != null ? $"&branchId={MainWindow.CurrentBranchId}" : "";
            _menu.AddRange(await App.Api.GetAsync<List<MenuItemDto>>("/api/v1/menu/items?onlyAvailable=true" + branchQ));
            foreach (var m in _menu)
            {
                var card = Ui.Card();
                card.Width = 200; card.Margin = new Thickness(0, 0, 12, 12);
                card.Cursor = System.Windows.Input.Cursors.Hand;
                card.Child = Ui.Column(
                    Ui.Label(m.Name, "#EEF1F7", 12.5, true) .TextWrapping(TextWrapping.Wrap),
                    Ui.Label(Ui.Money(m.Price), "#F4B942", 12.5, true) .Margin(0, 4, 0, 0),
                    Ui.Muted(Fa.Duration(m.PrepMinutes) + (m.IsVegetarian ? " • گیاهی" : "") + (m.IsSpicy ? " • 🌶 تند" : "")) .Margin(0, 3, 0, 0));
                card.MouseDown += (_, _) => { AddItem(m); };
                wrap.Children.Add(card);
            }
        }
        catch (Exception ex) { wrap.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }

    private void AddItem(MenuItemDto m)
    {
        _qty[m.Id] = _qty.GetValueOrDefault(m.Id) + 1;
        RepaintCart();
    }

    private void RepaintCart()
    {
        _cartPanel.Children.Clear();
        _total = 0;
        foreach (var (id, qty) in _qty)
        {
            var m = _menu.First(x => x.Id == id);
            _total += m.Price * qty;
            var notes = new TextBox { Width = 150, FontSize = 11, Padding = new Thickness(6, 4, 6, 4) };
            _noteBoxes[id] = notes;
            var row = Ui.Row(
                Ui.Label($"{Fa.Num(qty)}× {m.Name}", size: 12) .MaxWidth(130).TextTrimming(TextTrimming.CharacterEllipsis),
                Ui.MoneyEl(m.Price * qty).FontSize(11.5).HAlign(System.Windows.HorizontalAlignment.Right));
            _cartPanel.Children.Add(row);
            var notesRow = Ui.Row(Ui.Muted("یادداشت:") .Width(52), notes);
            notesRow.Margin = new Thickness(0, 2, 0, 8);
            _cartPanel.Children.Add(notesRow);
        }
        _cartPanel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 8) });
        _cartPanel.Children.Add(Ui.Row(Ui.Label("مبلغ کل", "#EEF1F7", 13, true),
            Ui.Label(Ui.Money(_total), "#4ADE80", 15, true) .HAlign(System.Windows.HorizontalAlignment.Right)));

        var submit = new Button { Content = "ارسال به آشپزخانه ←", Style = (Style)Application.Current.Resources["PrimaryBtn"], IsEnabled = _qty.Count > 0 };
        submit.Click += Submit_Click;
        _cartPanel.Children.Add(submit .Margin(0, 12, 0, 0));
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dto = new
            {
                type = 1,
                branchId = MainWindow.CurrentBranchId,
                tableId = _table.Id,
                items = _qty.Select(kv => new
                {
                    menuItemId = kv.Key,
                    quantity = kv.Value,
                    notes = _noteBoxes.TryGetValue(kv.Key, out var nb) && !string.IsNullOrWhiteSpace(nb.Text) ? nb.Text : null
                }).ToList()
            };
            await App.Api.PostAsync<OrderDto>("/api/v1/orders", dto);
            (Owner as MainWindow)?.Toast($"✅ سفارش میز {Fa.Num(_table.Number)} به آشپزخانه ارسال شد");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ثبت سفارش ناموفق بود", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
