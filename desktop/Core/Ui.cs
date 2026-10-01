using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartRestaurant.Desktop.Core;

/// <summary>Small UI builders shared by all dashboards (keeps view code terse).</summary>
public static class Ui
{
    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    public static TextBlock Label(string text, string color = "#EEF1F7", double size = 13, bool bold = false)
        => new TextBlock().Text(text).Foreground(Brush(color)).FontSize(size).FontWeight(bold ? FontWeights.SemiBold : FontWeights.Normal);

    public static TextBlock Muted(string text, double size = 11.5)
        => Label(text, "#9AA7C0", size);

    public static Border Card(UIElement? content = null, double padding = 16)
    {
        var b = new Border
        {
            Background = Brush("#171E2E"),
            BorderBrush = Brush("#2A3550"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(padding)
        };
        if (content != null) b.Child = new ContentControl { Content = content };
        return b;
    }

    public static StackPanel Column(params UIElement[] children)
    {
        var sp = new StackPanel();
        foreach (var c in children) sp.Children.Add(c);
        return sp;
    }

    public static StackPanel Row(params UIElement[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var c in children) sp.Children.Add(c);
        return sp;
    }

    public static StackPanel RowCenter(params UIElement[] children)
    {
        var sp = Row(children);
        sp.VerticalAlignment = VerticalAlignment.Center;
        return sp;
    }

    /// <summary>Big-number KPI card.</summary>
    public static UIElement StatCard(string icon, string title, string value, string accentColor, string? sub = null)
    {
        var stack = Column(
            Row(Label(icon, size: 20), Muted(title.ToUpper(), 10.5) .Margin(8, 4, 0, 0).FontWeight(FontWeights.SemiBold)),
            Label(value, "#EEF1F7", 24, bold: true) .Margin(0, 8, 0, 0));
        if (sub != null) stack.Children.Add(Muted(sub, 11) .Margin(0, 3, 0, 0));
        var b = Card(stack);
        b.BorderBrush = Brush(accentColor).Clone();
        b.BorderBrush.Opacity = 0.45;
        return b;
    }

    public static string Money(decimal v) => Fa.Money(v);
    public static string Money(double v) => Fa.Money(v);
    public static TextBlock MoneyEl(decimal v) => Label(Money(v));
    public static TextBlock MoneyEl(double v) => Label(Money(v));

    public static Border Badge(string text, string bg, string fg)
    {
        return new Border
        {
            Background = Brush(bg),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(9, 3, 9, 4),
            Child = Label(text, fg, 11, bold: true)
        };
    }

    public static TextBox TextField(string text = "", double width = 220)
        => new TextBox { Text = text, Width = width, HorizontalAlignment = HorizontalAlignment.Left };

    public static (Grid g, ComboBox cb) Combo(double width = 200)
    {
        var cb = new ComboBox { Width = width };
        return (new Grid(), cb);
    }

    public static TextBlock Placeholder(string text)
        => new TextBlock().Text(text).Foreground(Brush("#5B6B8C")).FontSize(13).HAlign(HorizontalAlignment.Center).Margin(0, 40, 0, 0);
}
