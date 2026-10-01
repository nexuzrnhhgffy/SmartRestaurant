using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Dialogs;

/// <summary>Cashier payment dialog — cash / card-present / online gateway (sandbox redirect opens in browser).</summary>
public class PaymentDialog : Window
{
    private readonly OrderDto _order;
    private readonly ComboBox _method = new ComboBox() .Width(250);
    private readonly TextBox _cashGiven = new TextBox() .Width(140);
    private readonly TextBlock _change = Ui.Label("", "#4ADE80", 15, true);
    private readonly Button _payBtn;

    public PaymentDialog(OrderDto order)
    {
        _order = order;
        Title = $"ثبت پرداخت — سفارش #{Fa.DigitsToFa(order.OrderNumber)}";
        Width = 520; Height = 470;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Ui.Brush("#0F1420");
        ResizeMode = ResizeMode.NoResize;
        FlowDirection = FlowDirection.RightToLeft;

        _method.ItemsSource = new List<string> { "💵 نقدی", "💳 کارتخوان (POS)", "🟢 زرین‌پال", "🔵 زیبال", "🟠 آی‌دی‌پی", "🟣 پی‌آی‌آر", "🔷 نکست‌پی", "🏦 بانک ملت", "🏦 بانک سامان", "🏦 بانک پارسیان" };
        _method.SelectedIndex = 0;
        _method.SelectionChanged += (_, _) => _change.Text = "";
        _cashGiven.TextChanged += (_, _) =>
        {
            if (decimal.TryParse(_cashGiven.Text, out var given) && _method.SelectedIndex == 0)
            {
                var change = given - order.Total;
                _change.Text = change >= 0 ? $"باقی‌مانده: {Ui.Money(change)}" : $"کسری: {Ui.Money(-change)}";
                _change.Foreground = change >= 0 ? Ui.Brush("#4ADE80") : Ui.Brush("#F87171");
            }
        };

        _payBtn = new Button { Content = "ثبت و تسویه پرداخت", Style = (Style)Application.Current.Resources["PrimaryBtn"], Padding = new Thickness(0, 11, 0, 11) };
        _payBtn.Click += Pay_Click;

        Content = new StackPanel { Margin = new Thickness(34, 28, 34, 28) };
        var sp = (StackPanel)Content;
        sp.Children.Add(Ui.Row(
            Ui.Label("سفارش #" + Fa.DigitsToFa(order.OrderNumber), "#F4B942", 20, true),
            Ui.Badge(Fa.TypeName(order.TypeName), "#22304A", "#EEF1F7") .Margin(12, 0, 0, 0).VAlign(VerticalAlignment.Center)));
        sp.Children.Add(Ui.Label(order.CustomerName != null ? $"مشتری: {order.CustomerName}" : "مشتری حضوری / سفارش میز", "#9AA7C0", 12) .Margin(0, 4, 0, 18));

        var totalCard = Ui.Card(Ui.Row(
            Ui.Label("مبلغ قابل پرداخت", "#9AA7C0", 12, true) .VAlign(VerticalAlignment.Center),
            Ui.Label(Ui.Money(order.Total), "#4ADE80", 26, true) .HAlign(System.Windows.HorizontalAlignment.Right).VAlign(VerticalAlignment.Center)))
        .Padding(18, 14, 18, 14).Margin(0, 0, 0, 18);
        sp.Children.Add(totalCard);

        var breakdown = Ui.Muted($"جمع آیتم‌ها {Ui.Money(order.SubTotal)}   •   تخفیف {Ui.Money(order.Discount)}−   •   مالیات {Ui.Money(order.Tax)}");
        sp.Children.Add(breakdown);

        sp.Children.Add(Ui.Label("روش پرداخت", "#9AA7C0", 11) .Margin(0, 18, 0, 6));
        sp.Children.Add(_method);

        var cashRow = Ui.Row(Ui.Label("وجه نقدی دریافتی  ", "#9AA7C0", 12) .VAlign(VerticalAlignment.Center).Width(120), _cashGiven, _change .Margin(12, 0, 0, 0));
        sp.Children.Add(cashRow);
        sp.Children.Add(Ui.Muted("درگاه‌های آنلاین صفحه بانک را در مرورگر باز می‌کنند؛ در حالت آزمایشی، پرداخت طی ۲/۵ ثانیه خودکار تأیید می‌شود.") .Margin(0, 14, 0, 0));

        sp.Children.Add(_payBtn .Margin(0, 20, 0, 0));
    }

    private async void Pay_Click(object sender, RoutedEventArgs e)
    {
        _payBtn.IsEnabled = false;
        _payBtn.Content = "در حال پردازش…";
        try
        {
            var idx = _method.SelectedIndex;
            if (idx <= 1)
            {
                await App.Api.PostAsync<object>("/api/v1/payments/initiate", new { orderId = _order.Id, gateway = idx == 0 ? 1 : 2 });
                (Owner as MainWindow)?.Toast("✅ پرداخت با " + (idx == 0 ? "نقدی" : "کارتخوان") + " ثبت شد");
                DialogResult = true;
            }
            else
            {
                var gateway = idx switch { 2 => 10, 3 => 11, 4 => 12, 5 => 13, 6 => 14, 7 => 20, 8 => 21, _ => 22 };
                var result = await App.Api.PostAsync<PaymentResultDto>("/api/v1/payments/initiate", new { orderId = _order.Id, gateway });
                if (result.IsSandbox)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.RedirectUrl) { UseShellExecute = true });
                    // poll until captured
                    for (int i = 0; i < 30; i++)
                    {
                        await Task.Delay(1000);
                        var o = await App.Api.GetAsync<OrderDto>($"/api/v1/orders/{_order.Id}");
                        if (o.PaymentStatusName == "Paid") break;
                    }
                    var o2 = await App.Api.GetAsync<OrderDto>($"/api/v1/orders/{_order.Id}");
                    if (o2.PaymentStatusName == "Paid") { (Owner as MainWindow)?.Toast("✅ " + result.GatewayName + " تسویه شد"); DialogResult = true; }
                    else (Owner as MainWindow)?.Toast("⏳ پرداخت آزمایشی هنوز تأیید نشده — چند لحظه دیگر بررسی کنید");
                }
                else
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.RedirectUrl) { UseShellExecute = true });
                    (Owner as MainWindow)?.Toast("🌐 صفحه بانک باز شد — پس از تأیید بانک، وضعیت را بروزرسانی کنید");
                    DialogResult = true;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "پرداخت ناموفق بود", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _payBtn.IsEnabled = true;
            _payBtn.Content = "ثبت و تسویه پرداخت";
        }
    }
}
