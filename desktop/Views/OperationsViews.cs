using System.Windows;
using System.Windows.Controls;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Dialogs;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Views;

/// <summary>Inventory — stock levels with reorder bars, receive stock, live movement ledger.</summary>
public class InventoryView : UserControl, IRefreshable
{
    private readonly StackPanel _list = new StackPanel();
    private readonly StackPanel _movements = new StackPanel();

    public InventoryView()
    {
        var receiveBtn = new Button { Content = "＋ Receive stock", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        receiveBtn.Click += async (_, _) => { new QuickFormDialog("Receive stock", new[] { "Ingredient (exact name)", "Quantity (+/−)", "Note" },
            async vals =>
            {
                var ings = await App.Api.GetAsync<List<IngredientDto>>("/api/v1/inventory/ingredients");
                var ing = ings.FirstOrDefault(i => i.Name.Equals(vals[0], StringComparison.OrdinalIgnoreCase))
                    ?? throw new ApiException("Ingredient not found: " + vals[0]);
                await App.Api.PostAsync<object>("/api/v1/inventory/movements", new { ingredientId = ing.Id, quantity = decimal.Parse(vals[1]), type = 1, note = vals[2] });
                (Application.Current.MainWindow as MainWindow)?.Toast("📦 Stock received: " + ing.Name);
            }) { Owner = Application.Current.MainWindow }.ShowDialog();
            await RefreshAsync();
        };

        var top = Ui.Row(Ui.Label("Stock levels — recipes auto-deduct on order completion", "#EEF1F7", 14, true), receiveBtn);

        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        var left = Ui.Card(Ui.Column(_list));
        left.Margin = new Thickness(0, 0, 12, 0);
        var right = Ui.Card(Ui.Column(Ui.Label("Movement ledger", "#EEF1F7", 13, true), _movements .Margin(0, 10, 0, 0)));
        grid.Children.Add(left);
        grid.Children.Add(right);
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sc.Content = grid;
        root.Children.Add(sc);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var ings = await App.Api.GetAsync<List<IngredientDto>>("/api/v1/inventory/ingredients");
            _list.Children.Clear();
            foreach (var i in ings)
            {
                var pct = i.MinStock <= 0 ? 100 : Math.Min(100, (double)(i.Stock / (i.MinStock * 3) * 100));
                var color = i.IsLow ? "#F87171" : pct < 40 ? "#F4B942" : "#4ADE80";
                var row = Ui.Column(
                    Ui.Row(
                        Ui.Label(i.Name, "#EEF1F7", 13, true) .Width(190),
                        Ui.Label($"{i.Stock:N1} / min {i.MinStock:N1} {i.Unit}", color, 12) .Width(150),
                        Ui.Muted(i.SupplierName ?? "") .Width(130),
                        Ui.Label(Ui.Money(i.StockValue), "#9AA7C0", 12) .HAlign(System.Windows.HorizontalAlignment.Right)),
                    new ProgressBar { Value = pct, Maximum = 100, Height = 5, Foreground = Ui.Brush(color), Background = Ui.Brush("#1E2740"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 5, 0, 10) });
                _list.Children.Add(row);
            }
            var totalValue = ings.Sum(i => i.StockValue);
            _list.Children.Insert(0, Ui.Row(Ui.Label($"Total stock value: ", "#9AA7C0", 12), Ui.Label(Ui.Money(totalValue), "#4ADE80", 13, true)) .Margin(0, 0, 0, 12));

            var moves = await App.Api.GetAsync<List<StockMovementDto>>("/api/v1/inventory/movements?take=40");
            _movements.Children.Clear();
            foreach (var m in moves)
            {
                var color = m.Quantity >= 0 ? "#4ADE80" : "#F87171";
                _movements.Children.Add(Ui.Column(
                    Ui.Row(
                        Ui.Label((m.Quantity >= 0 ? "+" : "") + m.Quantity.ToString("N1"), color, 12, true) .Width(62),
                        Ui.Label(m.IngredientName, size: 12) .Width(130).TextTrimming(TextTrimming.CharacterEllipsis),
                        Ui.Muted(m.OrderNumber ?? m.Type.ToString()) .Width(90)),
                    Ui.Muted(m.CreatedAt.ToLocalTime().ToString("MMM dd HH:mm")) .Margin(62, 1, 0, 7)));
            }
        }
        catch (Exception ex) { _list.Children.Clear(); _list.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}

/// <summary>Menu manager — cards with availability toggle, price edit, recipe (BOM) view.</summary>
public class MenuView : UserControl, IRefreshable
{
    private readonly WrapPanel _grid = new WrapPanel();

    public MenuView()
    {
        var root = new DockPanel();
        var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sc.Content = _grid;
        root.Children.Add(sc);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var items = await App.Api.GetAsync<List<MenuItemDto>>("/api/v1/menu/items");
            _grid.Children.Clear();
            foreach (var m in items)
            {
                var card = Ui.Card();
                card.Width = 250; card.Margin = new Thickness(0, 0, 14, 14);

                var toggle = new Button
                {
                    Content = m.IsAvailable ? "🟢 Available" : "🔴 86'd",
                    Style = (Style)Application.Current.Resources[m.IsAvailable ? "PrimaryBtn" : "DangerBtn"]
                };
                toggle.Click += async (_, _) =>
                {
                    await App.Api.PutAsync<object>($"/api/v1/menu/items/{m.Id}/availability", new { available = !m.IsAvailable });
                    await RefreshAsync();
                };

                var recipe = string.Join(", ", m.Recipe.Select(r => $"{r.Quantity:0.##}{r.Unit} {r.IngredientName}"));
                card.Child = Ui.Column(
                    Ui.Row(
                        Ui.Label(m.CategoryEmoji + "  " + m.Name, "#EEF1F7", 13, true) .TextWrapping(TextWrapping.Wrap).MaxWidth(170),
                        toggle .HAlign(System.Windows.HorizontalAlignment.Right).VAlign(VerticalAlignment.Top)),
                    Ui.Label(Ui.Money(m.Price), "#F4B942", 15, true) .Margin(0, 8, 0, 0),
                    Ui.Muted(m.Description ?? "") .TextWrapping(TextWrapping.Wrap).Margin(0, 3, 0, 0).MaxHeight(34),
                    Ui.Muted($"⏱ {m.PrepMinutes} min • {m.Calories} kcal" + (m.IsSpicy ? " • 🌶" : "") + (m.IsVegetarian ? " • 🌿" : "")) .Margin(0, 6, 0, 0),
                    Ui.Label(recipe.Length > 0 ? "Recipe: " + recipe : "No recipe → no stock deduction", "#5B6B8C", 10.5) .TextWrapping(TextWrapping.Wrap).Margin(0, 6, 0, 0));
                _grid.Children.Add(card);
            }
        }
        catch (Exception ex) { _grid.Children.Clear(); _grid.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}

/// <summary>Cameras — native MJPEG grid (unlimited cameras), add camera dialog.</summary>
public class CamerasView : UserControl, IRefreshable
{
    private readonly WrapPanel _grid = new WrapPanel();

    public CamerasView()
    {
        var addBtn = new Button { Content = "＋ Add camera / stream", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("Add camera", new[] { "Name", "URL (demo:kitchen / http://cam/mjpeg / rtsp://…)", "Location" },
                async vals =>
                {
                    var type = vals[1].StartsWith("rtsp", StringComparison.OrdinalIgnoreCase) ? 3 : vals[1].StartsWith("demo", StringComparison.OrdinalIgnoreCase) ? 4 : 1;
                    await App.Api.PostAsync<object>("/api/v1/cameras", new { name = vals[0], url = vals[1], location = vals[2], type, isActive = true });
                    (Application.Current.MainWindow as MainWindow)?.Toast("📹 Camera added");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };
        var top = Ui.Row(Ui.Label("Unlimited streams — demo feeds are synthesized server-side (zero hardware needed)", "#EEF1F7", 13, true), addBtn);
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sc.Content = _grid;
        root.Children.Add(sc);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var cams = await App.Api.GetAsync<List<CameraDto>>("/api/v1/cameras");
            _grid.Children.Clear();
            foreach (var c in cams)
            {
                var cell = Ui.Column();
                var view = new Controls.MjpegView { Height = 210 };
                view.StreamUrl = $"{App.Api.BaseUrl}/api/v1/cameras/{c.Id}/live";
                cell.Children.Add(view);
                cell.Children.Add(Ui.Row(
                    Ui.Label("●", "#F87171", 11),
                    Ui.Label("  " + c.Name, "#EEF1F7", 12.5, true),
                    Ui.Muted("  " + (c.Location ?? "") + " • " + c.Type)) .Margin(4, 6, 0, 0));
                var border = new Border { Child = cell, Margin = new Thickness(0, 0, 14, 14) };
                _grid.Children.Add(border);
            }
            if (cams.Count == 0) _grid.Children.Add(Ui.Placeholder("No cameras yet — add one above."));
        }
        catch (Exception ex) { _grid.Children.Clear(); _grid.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}
