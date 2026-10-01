using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartRestaurant.Desktop.Core;

/*
 * Fluent extensions — every property that dashboards set inline is available as
 * a chainable method so view code reads top-to-bottom.
 *   Ui.Muted("hi").Margin(0, 8, 0, 0).FontWeight(FontWeights.SemiBold)
 */
public static class Fluent
{
    // ── FrameworkElement ──
    public static T Margin<T>(this T fe, double l, double t, double r, double b) where T : FrameworkElement
    { fe.Margin = new Thickness(l, t, r, b); return fe; }
    public static T Width<T>(this T fe, double w) where T : FrameworkElement
    { fe.Width = w; return fe; }
    public static T MaxWidth<T>(this T fe, double w) where T : FrameworkElement
    { fe.MaxWidth = w; return fe; }
    public static T MaxHeight<T>(this T fe, double h) where T : FrameworkElement
    { fe.MaxHeight = h; return fe; }
    public static T MinWidth<T>(this T fe, double w) where T : FrameworkElement
    { fe.MinWidth = w; return fe; }
    public static T HRight<T>(this T fe) where T : FrameworkElement
    { fe.HorizontalAlignment = HorizontalAlignment.Right; return fe; }
    public static T HCenter<T>(this T fe) where T : FrameworkElement
    { fe.HorizontalAlignment = HorizontalAlignment.Center; return fe; }
    public static T VCenter<T>(this T fe) where T : FrameworkElement
    { fe.VerticalAlignment = VerticalAlignment.Center; return fe; }
    public static T Opacity<T>(this T fe, double o) where T : FrameworkElement
    { fe.Opacity = o; return fe; }

    public static T HAlign<T>(this T fe, System.Windows.HorizontalAlignment a) where T : FrameworkElement
    { fe.HorizontalAlignment = a; return fe; }
    public static T VAlign<T>(this T fe, System.Windows.VerticalAlignment a) where T : FrameworkElement
    { fe.VerticalAlignment = a; return fe; }

    // ── TextBlock ──
    public static TextBlock FontSize(this TextBlock tb, double s) { tb.FontSize = s; return tb; }
    public static TextBlock FontWeight(this TextBlock tb, FontWeight w) { tb.FontWeight = w; return tb; }
    public static TextBlock Foreground(this TextBlock tb, Brush b) { tb.Foreground = b; return tb; }
    public static TextBlock Text(this TextBlock tb, string s) { tb.Text = s; return tb; }
    public static TextBlock TextWrapping(this TextBlock tb, TextWrapping w) { tb.TextWrapping = w; return tb; }
    public static TextBlock TextTrimming(this TextBlock tb, TextTrimming t) { tb.TextTrimming = t; return tb; }
    public static TextBlock Background(this TextBlock tb, Brush b) { tb.Background = b; return tb; }
    public static TextBlock Padding(this TextBlock tb, double l, double t, double r, double b2) { tb.Padding = new Thickness(l, t, r, b2); return tb; }

    // ── Control ──
    public static T FontSize<T>(this T c, double s) where T : Control { c.FontSize = s; return c; }
    public static T FontWeight<T>(this T c, FontWeight w) where T : Control { c.FontWeight = w; return c; }
    public static T Foreground<T>(this T c, Brush b) where T : Control { c.Foreground = b; return c; }
    public static T Background<T>(this T c, Brush b) where T : Control { c.Background = b; return c; }
    public static T BorderBrush<T>(this T c, Brush b) where T : Control { c.BorderBrush = b; return c; }
    public static T BorderThickness<T>(this T c, Thickness th) where T : Control { c.BorderThickness = th; return c; }
    public static T Padding<T>(this T c, double l, double t, double r, double b) where T : Control
    { c.Padding = new Thickness(l, t, r, b); return c; }
    public static T Padding<T>(this T c, double uniform) where T : Control
    { c.Padding = new Thickness(uniform); return c; }
    public static T IsEnabled<T>(this T c, bool e) where T : Control { c.IsEnabled = e; return c; }

    // ── Border ──
    public static Border Background(this Border b, Brush br) { b.Background = br; return b; }
    public static Border BorderBrush(this Border b, Brush br) { b.BorderBrush = br; return b; }
    public static Border BorderThickness(this Border b, Thickness th) { b.BorderThickness = th; return b; }
    public static Border CornerRadius(this Border b, CornerRadius cr) { b.CornerRadius = cr; return b; }
    public static Border Padding(this Border b, double l, double t, double r, double bo) { b.Padding = new Thickness(l, t, r, bo); return b; }
    public static Border Padding(this Border b, double uniform) { b.Padding = new Thickness(uniform); return b; }
}
