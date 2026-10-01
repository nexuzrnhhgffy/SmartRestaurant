using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SmartRestaurant.Desktop.Controls;

/// <summary>Hand-drawn bar chart (no external charting dependency).</summary>
public class BarChart : System.Windows.FrameworkElement
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(List<(string label, double value)>), typeof(BarChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public List<(string label, double value)>? Items
    {
        get => (List<(string, double)>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight;
        if (w <= 0 || h <= 0 || Items == null || Items.Count == 0) return;

        var max = Math.Max(1, Items.Max(i => i.value));
        var areaTop = 16; var areaBottom = h - 34;
        var barArea = areaBottom - areaTop;
        double slot = w / Items.Count;
        double barW = Math.Min(38, slot * 0.55);

        // baseline + gridlines
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x35, 0x50)), 1);
        for (int g = 0; g <= 3; g++)
        {
            var y = areaTop + barArea * g / 3;
            dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }

        var accent = new LinearGradientBrush(Color.FromRgb(0xF4, 0xB9, 0x42), Color.FromRgb(0xE0, 0x8D, 0x1F), 90);
        var labelBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xC0));
        var valueBrush = new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF7));
        var labelFont = new Typeface(new FontFamily("Vazirmatn, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var valueFont = new Typeface(new FontFamily("Vazirmatn, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        for (int i = 0; i < Items.Count; i++)
        {
            var (label, value) = Items[i];
            var barH = barArea * value / max;
            var x = slot * i + (slot - barW) / 2;
            var y = areaBottom - barH;

            if (barH > 2)
            {
                var rect = new Rect(x, y, barW, barH);
                dc.PushClip(new RectangleGeometry(rect, 6, 6));
                dc.DrawRectangle(accent, null, rect);
                dc.Pop();
            }

            // value on top
            if (value > 0)
            {
                var ft = new FormattedText(FormatShort(value), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    valueFont, 10, valueBrush, 1.25);
                dc.DrawText(ft, new Point(x + barW / 2 - ft.Width / 2, y - 15));
            }
            // label below
            var lt = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                labelFont, 10, labelBrush, 1.25);
            dc.DrawText(lt, new Point(x + barW / 2 - lt.Width / 2, areaBottom + 8));
        }
    }

    internal static string FormatShort(double v)
    {
        if (v >= 1_000_000) return (v / 1_000_000).ToString("0.#") + "M";
        if (v >= 1_000) return (v / 1_000).ToString("0.#") + "K";
        return v.ToString("0");
    }
}

/// <summary>Donut/ring chart for proportions (payment mix etc.).</summary>
public class DonutChart : System.Windows.FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(List<(string label, double value, Color color)>), typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public List<(string label, double value, Color color)>? Segments
    {
        get => (List<(string, double, Color)>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight;
        if (w <= 0 || h <= 0 || Segments == null || Segments.Count == 0) return;
        var total = Segments.Sum(s => s.value);
        if (total <= 0) return;

        double cx = w / 2, cy = h / 2;
        double r = Math.Min(w, h) / 2 - 10;
        double thickness = r * 0.32;
        double start = -90;

        foreach (var (_, value, color) in Segments)
        {
            var sweep = 360.0 * value / total;
            var pen = new Pen(new SolidColorBrush(color), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            double mid = start + sweep / 2;
            var rad0 = mid * Math.PI / 180;
            var p1 = new Point(cx + (r - thickness / 2) * Math.Cos(rad0), cy + (r - thickness / 2) * Math.Sin(rad0));
            var rad1 = (mid + Math.Max(0.5, sweep - 2)) * Math.PI / 180;
            var p2 = new Point(cx + (r - thickness / 2) * Math.Cos(rad1), cy + (r - thickness / 2) * Math.Sin(rad1));
            if (sweep > 4) dc.DrawLine(pen, p1, p2);
            start += sweep;
        }

        var centerFont = new Typeface(new FontFamily("Vazirmatn, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var ft = new FormattedText(BarChart.FormatShort(total), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            centerFont, 16, new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF7)), 1.25);
        dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }
}
