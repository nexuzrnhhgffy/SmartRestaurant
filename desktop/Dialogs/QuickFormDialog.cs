using System.Windows;
using System.Windows.Controls;
using SmartRestaurant.Desktop.Core;
using SmartRestaurant.Desktop.Windows;

namespace SmartRestaurant.Desktop.Dialogs;

/// <summary>Generic small form dialog (n labels → string values) used by several dashboards.</summary>
public class QuickFormDialog : Window
{
    private readonly TextBox[] _inputs;
    private readonly Func<string[], Task> _onSubmit;

    public QuickFormDialog(string title, string[] labels, Func<string[], Task> onSubmit)
    {
        Title = title;
        Width = 440; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Ui.Brush("#0F1420");
        FlowDirection = FlowDirection.RightToLeft;
        _inputs = labels.Select(l => new TextBox()).ToArray();
        _onSubmit = onSubmit;

        var sp = new StackPanel { Margin = new Thickness(30, 24, 30, 24) };
        sp.Children.Add(Ui.Label(title, "#F4B942", 17, true));
        sp.Children.Add(new TextBlock());
        for (int i = 0; i < labels.Length; i++)
        {
            sp.Children.Add(Ui.Label(labels[i], "#9AA7C0", 11) .Margin(0, 14, 0, 5));
            sp.Children.Add(_inputs[i]);
        }
        var ok = new Button { Content = "ثبت اطلاعات", Style = (Style)Application.Current.Resources["PrimaryBtn"], Padding = new Thickness(0, 10, 0, 10), Margin = new Thickness(0, 22, 0, 0) };
        ok.Click += async (_, _) =>
        {
            try
            {
                ok.IsEnabled = false;
                await _onSubmit(_inputs.Select(x => x.Text.Trim()).ToArray());
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                ok.IsEnabled = true;
            }
        };
        sp.Children.Add(ok);
        Content = sp;
    }
}
