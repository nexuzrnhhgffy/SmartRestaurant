using HarfBuzzSharp;
using SkiaSharp;

namespace SmartRestaurant.Application.Printing;

/// <summary>
/// HarfBuzz-shaped text drawing for Persian (RTL) on SkiaSharp canvases.
/// Raw canvas.DrawText() renders Arabic-script strings in logical order →
/// reversed, unjoined glyphs. HarfBuzz produces correct visual order,
/// contextual joining (initial/medial/final forms) and bidi runs.
/// </summary>
public static class RtlText
{
    private sealed class HbEntry : IDisposable
    {
        public required SKData Data;
        public required Blob Blob;
        public required Face Face;
        public required Font Font;
        public void Dispose() { Font.Dispose(); Face.Dispose(); Blob.Dispose(); Data.Dispose(); }
    }

    private static readonly Dictionary<SKTypeface, HbEntry> FontCache = new();

    private static HbEntry Entry(SKTypeface typeface)
    {
        lock (FontCache)
        {
            if (FontCache.TryGetValue(typeface, out var e)) return e;
            var stream = typeface.OpenStream();
            var data = SKData.Create(stream);
            var blob = new Blob(data.Data, (int)data.Size, MemoryMode.ReadOnly, null);
            var face = new Face(blob, 0);
            var font = new Font(face);
            e = new HbEntry { Data = data, Blob = blob, Face = face, Font = font };
            FontCache[typeface] = e;
            return e;
        }
    }

    /// <summary>Shape text → SKTextBlob in visual order (pixel positions).</summary>
    public static SKTextBlob Shape(string text, SKFont skFont, out float width)
    {
        var typeface = skFont.Typeface ?? SKTypeface.Default;
        var entry = Entry(typeface);
        float size = skFont.Size;
        // scale so advances come out in pixels (upem → px)
        int upem = (int)entry.Face.UnitsPerEm;
        entry.Font.SetScale(upem, upem);

        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf8(text);
        buffer.GuessSegmentProperties();
        entry.Font.Shape(buffer);

        var infos = buffer.GlyphInfos;
        var poss = buffer.GlyphPositions;
        float pxPerUnit = size / upem;

        var builder = new SKTextBlobBuilder();
        var run = builder.AllocatePositionedRun(skFont, infos.Length, null);
        var glyphs = run.GetGlyphSpan();
        var positions = ((SKPositionedRunBuffer)run).GetPositionSpan();

        float x = 0;
        for (int i = 0; i < infos.Length; i++)
        {
            glyphs[i] = (ushort)infos[i].Codepoint;
            positions[i] = new SKPoint(x + poss[i].XOffset * pxPerUnit, -poss[i].YOffset * pxPerUnit);
            x += poss[i].XAdvance * pxPerUnit;
        }
        width = x;
        return builder.Build();
    }

    /// <summary>Draw RTL-shaped text (font-based overload).</summary>
    public static void Draw(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var blob = Shape(text, font, out _);
        canvas.DrawText(blob, x, y, paint);
    }

    /// <summary>Draw RTL-shaped text (paint-based overload).</summary>
    public static void Draw(SKCanvas canvas, string text, float x, float y, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var font = new SKFont(paint.Typeface ?? SKTypeface.Default, paint.TextSize);
        using var blob = Shape(text, font, out _);
        canvas.DrawText(blob, x, y, paint);
    }

    /// <summary>Visual width in pixels (for centering).</summary>
    public static float Measure(string text, SKFont font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        using var blob = Shape(text, font, out var w);
        return w;
    }
}
