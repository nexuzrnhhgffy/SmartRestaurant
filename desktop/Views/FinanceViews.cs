using System.Windows;
using System.Windows.Controls;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Dialogs;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Views;

/// <summary>Accounting — P&L summary, journal entries, expenses with auto-journal.</summary>
public class AccountingView : UserControl, IRefreshable
{
    private readonly StackPanel _cards = new StackPanel();
    private readonly StackPanel _journals = new StackPanel();
    private readonly StackPanel _expenses = new StackPanel();

    public AccountingView()
    {
        var addBtn = new Button { Content = "＋ ثبت هزینه", Style = (Style)Application.Current.Resources["PrimaryBtn"] };
        addBtn.Click += (_, _) =>
        {
            new QuickFormDialog("ثبت هزینه", new[] { "عنوان", "دسته (اجاره/حقوق/قبض/تجهیزات)", "مبلغ (تومان)", "پرداخت به" },
                async vals =>
                {
                    await App.Api.PostAsync<object>("/api/v1/accounting/expenses", new
                    { title = vals[0], category = vals[1], amount = decimal.Parse(vals[2]), spentAt = DateTime.UtcNow, paidTo = vals[3] });
                    (Application.Current.MainWindow as MainWindow)?.Toast("🧾 هزینه ثبت شد + سند حسابداری صادر گردید");
                }) { Owner = Application.Current.MainWindow }.ShowDialog();
        };

        var top = Ui.Row(Ui.Label("سود و زیان — ۳۰ روز اخیر", "#EEF1F7", 14, true), addBtn);
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });

        var pl = Ui.Card(Ui.Column(_cards));
        var jr = Ui.Card(Ui.Column(Ui.Label("اسناد حسابداری (دوطرفته)", "#EEF1F7", 13, true), _journals .Margin(0, 10, 0, 0)));
        jr.Margin = new Thickness(12, 0, 12, 0);
        var ex = Ui.Card(Ui.Column(Ui.Label("هزینه‌ها", "#EEF1F7", 13, true), _expenses .Margin(0, 10, 0, 0)));
        grid.Children.Add(pl); grid.Children.Add(jr); grid.Children.Add(ex);
        Grid.SetColumn(pl, 0); Grid.SetColumn(jr, 1); Grid.SetColumn(ex, 2);

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
            var pl = await App.Api.GetAsync<ProfitLossDto>("/api/v1/accounting/profit-loss");
            _cards.Children.Clear();
            void Stat(string title, decimal val, string color) => _cards.Children.Add(Ui.Column(
                Ui.Muted(title.ToUpper(), 10.5),
                Ui.Label(Ui.Money(val), color, 21, true) .Margin(0, 3, 0, 12)));
            Stat("درآمد (سفارش‌های تکمیل‌شده)", pl.Revenue, "#4ADE80");
            Stat("مالیات ارزش افزوده پرداختنی", pl.VatPayable, "#F4B942");
            Stat("هزینه‌های عملیاتی", pl.Expenses, "#F87171");
            Stat("سود خالص", pl.NetProfit, pl.NetProfit >= 0 ? "#4ADE80" : "#F87171");
            _cards.Children.Add(Ui.Muted($"{Fa.Num(pl.OrdersCount)} سفارش • میانگین هر فاکتور {Ui.Money(pl.AvgTicket)}"));

            var journals = await App.Api.GetAsync<List<JournalEntryDto>>("/api/v1/accounting/journals?take=20");
            _journals.Children.Clear();
            foreach (var j in journals)
            {
                var lines = string.Join("   ", j.Lines.Select(l => $"{l.AccountCode} {(l.Debit > 0 ? "بد" : "بس")}{Fa.Num(l.Debit > 0 ? l.Debit : l.Credit)}"));
                _journals.Children.Add(Ui.Column(
                    Ui.Row(Ui.Label(Fa.DigitsToFa(j.EntryNumber), "#F4B942", 11.5, true), Ui.Muted("  " + j.SourceType ?? "") , Ui.Muted(Fa.Jalali(j.PostedAt.ToLocalTime())) .HAlign(System.Windows.HorizontalAlignment.Right)),
                    Ui.Label(lines, "#9AA7C0", 11.5) .Margin(0, 2, 0, 0),
                    Ui.Muted(j.Description) .Margin(0, 1, 0, 8)));
            }

            var expenses = await App.Api.GetAsync<List<ExpenseDto>>("/api/v1/accounting/expenses");
            _expenses.Children.Clear();
            foreach (var e in expenses.Take(15))
                _expenses.Children.Add(Ui.Row(
                    Ui.Label(e.Title, size: 12) .Width(150).TextTrimming(TextTrimming.CharacterEllipsis),
                    Ui.Muted(e.Category ?? "") .Width(70),
                    Ui.Label(Ui.Money(e.Amount), "#F87171", 12) .HAlign(System.Windows.HorizontalAlignment.Right)));
            if (expenses.Count == 0) _expenses.Children.Add(Ui.Muted("هنوز هزینه‌ای ثبت نشده است."));
        }
        catch (Exception ex) { _cards.Children.Clear(); _cards.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}

/// <summary>Payments ledger + gateway config toggles (SuperAdmin) + X report.</summary>
public class PaymentsView : UserControl, IRefreshable
{
    private readonly StackPanel _list = new StackPanel();
    private readonly StackPanel _gateways = new StackPanel();
    private readonly StackPanel _xreport = new StackPanel();

    public PaymentsView()
    {
        var xBtn = new Button { Content = "🧾 گزارش X (امروز)" };
        xBtn.Click += async (_, _) =>
        {
            try
            {
                var x = await App.Api.GetAsync<XReportDto>("/api/v1/reports/x-report");
                _xreport.Children.Clear();
                void L(string k, string v) => _xreport.Children.Add(Ui.Row(Ui.Muted(k), Ui.Label(v, "#EEF1F7", 12, true) .HAlign(System.Windows.HorizontalAlignment.Right)));
                L("شعبه", x.BranchName);
                L("سفارش‌ها / مهمان", $"{Fa.Num(x.OrdersCount)} / {Fa.Num(x.Guests)}");
                L("فروش ناخالص", Ui.Money(x.GrossSales));
                L("تخفیف‌ها", "−" + Ui.Money(x.Discounts));
                L("مالیات ارزش افزوده", Ui.Money(x.Vat));
                L("فروش خالص", Ui.Money(x.NetSales));
                L("نقدی", Ui.Money(x.CashCollected));
                L("درگاه‌های آنلاین", Ui.Money(x.OnlineCollected));
                L("کارتخوان (POS)", Ui.Money(x.CardCollected));
                L("سفارش‌های لغوشده", Fa.Num(x.Voids));
            }
            catch (Exception ex) { (Application.Current.MainWindow as MainWindow)?.Toast("⚠ " + ex.Message); }
        };

        var top = Ui.Row(Ui.Label("دفتر پرداخت‌ها و درگاه‌ها", "#EEF1F7", 14, true), xBtn);
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });

        var l1 = Ui.Card(Ui.Column(_list));
        var l2 = Ui.Card(Ui.Column(Ui.Label("وضعیت درگاه‌ها", "#EEF1F7", 13, true), _gateways .Margin(0, 10, 0, 0)));
        l2.Margin = new Thickness(12, 0, 12, 0);
        var l3 = Ui.Card(Ui.Column(Ui.Label("گزارش X", "#EEF1F7", 13, true), _xreport .Margin(0, 10, 0, 0)));
        grid.Children.Add(l1); grid.Children.Add(l2); grid.Children.Add(l3);
        Grid.SetColumn(l1, 0); Grid.SetColumn(l2, 1); Grid.SetColumn(l3, 2);

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var sc = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sc.Content = grid;
        root.Children.Add(sc);
        Content = root;
        App.Realtime.PaymentCaptured += _ => Dispatcher.BeginInvoke(async () => await RefreshAsync());
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var pays = await App.Api.GetAsync<List<PaymentDto>>("/api/v1/payments?take=40");
            _list.Children.Clear();
            foreach (var p in pays)
            {
                var color = p.Status == 3 ? "#4ADE80" : p.Status == 4 ? "#F87171" : "#F4B942";
                _list.Children.Add(Ui.Row(
                    Ui.Label("#" + Fa.DigitsToFa(p.OrderNumber), "#F4B942", 12.5, true),
                    Ui.Label("  " + p.GatewayName, size: 12),
                    Ui.Badge(p.Status == 3 ? "پرداخت شد" : p.Status == 4 ? "ناموفق" : "در انتظار", "#22304A", color),
                    Ui.Label(p.RefId != null ? " پیگیری: " + Fa.DigitsToFa(p.RefId) : "", "#5B6B8C", 11),
                    Ui.Label(Ui.Money(p.Amount), "#EEF1F7", 12.5, true) .HAlign(System.Windows.HorizontalAlignment.Right),
                    Ui.Muted("  " + Fa.JalaliTime(p.CreatedAt.ToLocalTime())) .HAlign(System.Windows.HorizontalAlignment.Right)));
            }
            _list.Margin = new Thickness(0, 4, 0, 0);
            if (pays.Count == 0) _list.Children.Add(Ui.Placeholder("هنوز پرداختی ثبت نشده است."));

            var gws = await App.Api.GetAsync<List<GatewayConfigDto>>("/api/v1/payments/gateways");
            _gateways.Children.Clear();
            foreach (var g in gws)
            {
                var chip = g.Enabled
                    ? Ui.Badge(g.Sandbox ? "آزمایشی" : "عملیاتی", g.Sandbox ? "#3A2F14" : "#15301F", g.Sandbox ? "#F4B942" : "#4ADE80")
                    : Ui.Badge("غیرفعال", "#222", "#888");
                _gateways.Children.Add(Ui.Row(Ui.Label(Fa.Gateway(g.GatewayName), size: 12), chip .HAlign(System.Windows.HorizontalAlignment.Right)));
            }
            _gateways.Children.Add(Ui.Muted("حالت آزمایشی: صفحه بانک به‌صورت خودکار تأیید می‌کند. برای رفتن به حالت عملیاتی، کلیدها را در تنظیمات وارد کنید.") .Margin(0, 12, 0, 0).TextWrapping(TextWrapping.Wrap));
        }
        catch (Exception ex) { _list.Children.Clear(); _list.Children.Add(Ui.Label("⚠ " + ex.Message, "#F87171", 12)); }
    }
}
