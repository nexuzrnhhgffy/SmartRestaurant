using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SmartRestaurant.Desktop.Controls;

/*
 * Native MJPEG camera viewer — no external dependencies (no VLC, no WebView2).
 * Streams multipart/x-mixed-replace from the server camera proxy, decodes each
 * JPEG frame into a BitmapImage, and auto-reconnects on failure.
 */
public class MjpegView : Border
{
    private readonly Image _image;
    private readonly TextBlock _status;
    private CancellationTokenSource? _cts;
    private static readonly HttpClient Http = new HttpClient() { Timeout = Timeout.InfiniteTimeSpan };
    private string? _url;

    public MjpegView()
    {
        CornerRadius = new CornerRadius(12);
        Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0E, 0x18));
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x35, 0x50));
        BorderThickness = new Thickness(1);
        ClipToBounds = true;

        _image = new Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
        _status = new TextBlock
        {
            Text = "connecting…",
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xC0)),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var grid = new Grid();
        grid.Children.Add(_image);
        grid.Children.Add(_status);
        Child = grid;
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
    }

    public string? StreamUrl
    {
        get => _url;
        set { _url = value; Stop(); if (IsLoaded) Start(); }
    }

    public void Start()
    {
        if (_url == null) return;
        Stop();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => StreamLoopAsync(_url, _cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task StreamLoopAsync(string url, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                SetStatus("connecting…");
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                res.EnsureSuccessStatusCode();
                await using var stream = await res.Content.ReadAsStreamAsync(ct);

                var buffer = new List<byte>(64 * 1024);
                var chunk = new byte[16384];
                SetStatus("LIVE");
                Opacity = 1;
                while (!ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(chunk, ct);
                    if (read == 0) break;
                    buffer.AddRange(chunk.Take(read));

                    // extract every complete JPEG (SOI 0xFFD8 … EOI 0xFFD9)
                    while (true)
                    {
                        var soi = IndexOf(buffer, 0, new byte[] { 0xFF, 0xD8 });
                        if (soi < 0) { buffer.Clear(); break; }
                        var eoi = IndexOf(buffer, soi + 2, new byte[] { 0xFF, 0xD9 });
                        if (eoi < 0)
                        {
                            if (soi > 0) buffer.RemoveRange(0, soi);
                            if (buffer.Count > 4 * 1024 * 1024) buffer.Clear(); // runaway
                            break;
                        }
                        int frameLen = eoi - soi + 2;
                        var frame = buffer.GetRange(soi, frameLen).ToArray();
                        buffer.RemoveRange(0, eoi + 2);
                        ShowFrame(frame);
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch { }
            SetStatus("reconnecting…");
            try { await Task.Delay(2500, ct); } catch (OperationCanceledException) { return; }
        }
    }

    private void ShowFrame(byte[] jpeg)
    {
        try
        {
            var img = new BitmapImage();
            using var ms = new MemoryStream(jpeg);
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            Dispatcher.BeginInvoke(() =>
            {
                _image.Source = img;
                _image.Opacity = 1;
            });
        }
        catch { }
    }

    private void SetStatus(string text) => Dispatcher.BeginInvoke(() => _status.Text = text);

    private static int IndexOf(List<byte> haystack, int start, byte[] needle)
    {
        for (int i = start; i <= haystack.Count - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }
}
