using SkiaSharp;
using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Application.Printing;

/*
 * ─────────────────── RASTER RECEIPT RENDERER (Persian-safe) ───────────────────
 * Most thermal printers ship with Chinese/European codepages and mangle Persian
 * glyphs in text mode. The bulletproof industry trick: render the receipt to a
 * 1-bpp bitmap (with real font shaping for RTL) and send it as a GS v 0 raster
 * image — pixel-identical output on every ESC/POS printer.
 */
public static class SkiaReceiptRenderer
{
    private const int PaperWidth = 576;   // 80mm @ 203dpi (384 for 58mm)
    private const int Margin = 24;

    private static SKTypeface? LoadTypeface()
    {
        // file candidates first (Linux/CI), then family names (Windows/macOS)
        string[] files =
        {
            "/usr/share/fonts/truetype/vazirmatn/Vazirmatn-Regular.ttf",
            "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/chinese/NotoSansSC-Regular.ttf",
        };
        foreach (var path in files)
        {
            try { if (File.Exists(path) && SKTypeface.FromFile(path) is { } tf) return tf; }
            catch { /* corrupt/unsupported file — try next */ }
        }
        string[] families = { "Vazirmatn", "Tahoma", "Segoe UI", "FreeSerif", "DejaVu Sans", "Arial", "sans-serif" };
        foreach (var f in families)
        {
            try { if (SKTypeface.FromFamilyName(f) is { } tf && tf.FamilyName != null) return tf; }
            catch { }
        }
        return SKTypeface.FromFamilyName(null); // Skia default — never null in practice
    }

    public static (byte[] raster, int width, int height) RenderToRaster(object data)
    {
        var lines = data switch
        {
            ReceiptData r => ReceiptLines(r),
            KitchenTicket k => TicketLines(k),
            _ => throw new ArgumentException("Unsupported receipt model")
        };

        using var tf = LoadTypeface() ?? throw new InvalidOperationException("No font available for raster receipt");
        using var font = new SKFont(tf, 26);
        using var fontBig = new SKFont(tf, 34);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        // measure
        int width = PaperWidth, y = Margin;
        var metrics = new List<(string text, SKFont f, bool center, int y, int h)>();
        foreach (var (text, big, center) in lines)
        {
            var f = big ? fontBig : font;
            var h = (int)Math.Ceiling(f.Metrics.Descent - f.Metrics.Ascent) + 6;
            metrics.Add((text, big ? fontBig : font, center, y, h));
            y += h;
        }
        int height = y + Margin + 40;

        using var bmp = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        foreach (var (text, f, center, ly, lh) in metrics)
        {
            if (string.IsNullOrEmpty(text)) continue;
            var w = RtlText.Measure(text, f);
            var x = center ? (width - w) / 2f : Margin;
            var baseline = ly + (-f.Metrics.Ascent);
            RtlText.Draw(canvas, text, x, baseline, f, paint);
        }

        // 1bpp pack: 8 px per byte, MSB first
        int rowBytes = (width + 7) / 8;
        var raster = new byte[rowBytes * height];
        var pixels = bmp.Pixels;
        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                var c = pixels[py * width + px];
                bool black = (c.Red + c.Green + c.Blue) / 3 < 128;
                if (black)
                {
                    int idx = py * rowBytes + px / 8;
                    raster[idx] |= (byte)(0x80 >> (px % 8));
                }
            }
        }
        return (raster, width, height);
    }

    private static List<(string text, bool big, bool center)> ReceiptLines(ReceiptData r)
    {
        var L = new List<(string, bool, bool)>
        {
            (r.RestaurantName, true, true),
            ($"شعبه: {r.BranchName}", false, true),
            ("────────────────────────────", false, true),
            ($"شماره سفارش: {r.OrderNumber}", false, false),
            ($"تاریخ: {Fa.JalaliTime(r.CompletedAt.ToLocalTime())}", false, false),
            ("--------------------------------", false, false),
        };
        foreach (var l in r.Lines)
            L.Add(($"{Fa.Num(l.Quantity)}× {l.Name}   {Fa.Num(l.Total)}", false, false));
        L.Add(("--------------------------------", false, false));
        L.Add(($"جمع کل: {Fa.Num(r.SubTotal)} تومان", false, false));
        if (r.Discount > 0) L.Add(($"تخفیف: {Fa.Num(r.Discount)} تومان کسر می‌گردد", false, false));
        L.Add(($"مالیات بر ارزش افزوده: {Fa.Num(r.Tax)} تومان", false, false));
        L.Add(($"مبلغ نهایی: {Fa.Num(r.Total)} تومان", true, false));
        L.Add(($"روش پرداخت: {r.PaymentMethod} {(r.RefId != null ? "/ شماره پیگیری: " + r.RefId : "")}", false, true));
        L.Add((r.Footer, false, true));
        L.Add(("سیستم جامع مدیریت رستوران زعفران", false, true));
        return L;
    }

    private static List<(string text, bool big, bool center)> TicketLines(KitchenTicket t)
    {
        var L = new List<(string, bool, bool)>
        {
            ($"#{t.OrderNumber}", true, true),
            ($"{t.TableOrType}   {Fa.Time(t.CreatedAt.ToLocalTime())}", false, true),
            ("════════════════════════", false, true),
        };
        foreach (var l in t.Lines)
        {
            L.Add(($"{l.Quantity}× {l.Name}", true, false));
            if (!string.IsNullOrWhiteSpace(l.Notes)) L.Add((l.Notes, false, false));
        }
        L.Add(("════════════════════════", false, true));
        return L;
    }
}

/// <summary>Sends ESC/POS bytes to a network thermal printer (port 9100 raw TCP).</summary>
public static class NetworkPrinterSender
{
    public static async Task SendAsync(string host, int port, byte[] payload, CancellationToken ct = default)
    {
        using var client = new System.Net.Sockets.TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));
        await client.ConnectAsync(host, port, timeoutCts.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(payload, 0, payload.Length, ct);
        await stream.FlushAsync(ct);
    }
}
