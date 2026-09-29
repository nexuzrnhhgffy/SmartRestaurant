using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace SmartRestaurant.Api.Middleware;

/*
 * ═══════════════════ MJPEG STREAMING ENGINE ═══════════════════
 * multipart/x-mixed-replace writer with two sources:
 *   1) DemoMjpegStream — synthesizes animated surveillance frames (SkiaSharp):
 *      moving "staff" dots, ticking clock, camera label, scanline effect.
 *      Lets the whole camera feature work out-of-the-box with zero hardware.
 *   2) ProxyMjpegStream — relays any LAN/Internet MJPEG camera (fixes CORS +
 *      mixed-content so the web dashboards can show camera feeds directly).
 */
public static class DemoMjpegStream
{
    public static async Task WriteAsync(HttpResponse res, string name, string url, CancellationToken ct)
    {
        var theme = url switch
        {
            var u when u.Contains("grill") => (new SKColor(150, 60, 40), new SKColor(40, 20, 14)),
            var u when u.Contains("hall") => (new SKColor(40, 70, 120), new SKColor(14, 22, 36)),
            var u when u.Contains("cashier") => (new SKColor(120, 100, 30), new SKColor(30, 26, 12)),
            _ => (new SKColor(70, 90, 60), new SKColor(18, 26, 16)),
        };

        res.ContentType = "multipart/x-mixed-replace; boundary=frame";
        await res.Body.FlushAsync(ct);

        int t = 0;
        while (!ct.IsCancellationRequested)
        {
            var bytes = RenderFrameJpeg(name, theme.Item1, theme.Item2, t);
            await res.WriteAsync($"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {bytes.Length}\r\n\r\n", ct);
            await res.Body.WriteAsync(bytes, ct);
            await res.Body.FlushAsync(ct);
            await Task.Delay(250, ct); // ~4 fps
            t++;
        }
    }

    private static byte[] RenderFrameJpeg(string label, SKColor accent, SKColor bg, int t)
    {
        int w = 640, h = 360;
        var bmp = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bmp);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.Clear(bg);

        // floor grid (perspective feel)
        paint.Color = accent.WithAlpha(40); paint.StrokeWidth = 1;
        for (int i = 0; i <= 10; i++) canvas.DrawLine(0, h * 0.45f + i * (h * 0.55f / 10f), w, h * 0.45f + i * (h * 0.55f / 10f), paint);
        for (int i = 0; i <= 14; i++) canvas.DrawLine(w * 0.5f + (i - 7) * 24, h * 0.45f, w * 0.5f + (i - 7) * 90, h, paint);

        // moving "people" dots with heat trails
        var rand = new Random(42);
        for (int p = 0; p < 5; p++)
        {
            float px = (float)((Math.Sin((t + p * 37) / 22.0) + 1) / 2 * (w - 80) + 40);
            float py = h * 0.5f + (p * 34f) % (h * 0.4f);
            paint.Color = SKColors.White.WithAlpha(220);
            canvas.DrawCircle(px, py, 9, paint);
            paint.Color = accent.WithAlpha(90);
            canvas.DrawCircle(px, py, 16, paint);
        }

        // kitchen equipment silhouettes
        paint.Color = accent.WithAlpha(120);
        canvas.DrawRoundRect(40, 40, 160, 60, 8, 8, paint);
        canvas.DrawRoundRect(w - 200, 40, 160, 60, 8, 8, paint);
        using var font = new SKFont(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 16);
        using var tp = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText("STATION", 60, 78, font, tp);
        canvas.DrawText("EXIT", w - 176, 78, font, tp);

        // scanline overlay
        paint.Color = SKColors.Black.WithAlpha(30);
        for (int y = (t * 3) % 6; y < h; y += 6) canvas.DrawRect(0, y, w, 2, paint);

        // HUD: REC dot, clock, label
        paint.Color = SKColors.Red;
        canvas.DrawCircle(w - 26, 24, 7, paint);
        canvas.DrawText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), w - 250, 30, font, tp);
        canvas.DrawText(label.ToUpperInvariant(), 18, h - 18, font, tp);
        canvas.DrawText("LIVE • 4FPS • 640x360", 18, h - 42, new SKFont(SKTypeface.Default, 12), new SKPaint { Color = SKColors.White.WithAlpha(160), IsAntialias = true });

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Jpeg, 70);
        return data.ToArray();
    }
}

public static class ProxyMjpegStream
{
    public static async Task WriteAsync(HttpResponse res, string cameraUrl, CancellationToken ct)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        using var upstream = await http.GetAsync(cameraUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct);
        res.ContentType = upstream.Content.Headers.ContentType?.ToString() ?? "multipart/x-mixed-replace; boundary=frame";
        await using var src = await upstream.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[8192];
        while (!ct.IsCancellationRequested)
        {
            int read = await src.ReadAsync(buffer, ct);
            if (read == 0) break;
            await res.Body.WriteAsync(buffer, 0, read, ct);
            await res.Body.FlushAsync(ct);
        }
    }
}
