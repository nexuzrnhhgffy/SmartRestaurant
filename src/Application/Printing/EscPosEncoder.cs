using System.Globalization;
using System.Text;

namespace SmartRestaurant.Application.Printing;

/// <summary>Data for a kitchen ticket.</summary>
public class KitchenTicket
{
    public string OrderNumber { get; set; } = default!;
    public string? TableOrType { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Customer { get; set; }
    public List<KitchenTicketLine> Lines { get; set; } = new();
    public string? Note { get; set; }
}

public class KitchenTicketLine
{
    public string Name { get; set; } = default!;
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Data for the guest receipt (paid invoice).</summary>
public class ReceiptData
{
    public string RestaurantName { get; set; } = "Zafaran Restaurant";
    public string BranchName { get; set; } = default!;
    public string OrderNumber { get; set; } = default!;
    public DateTime CompletedAt { get; set; }
    public string? Cashier { get; set; }
    public List<ReceiptLine> Lines { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? PaymentMethod { get; set; }
    public string? RefId { get; set; }
    public string Footer { get; set; } = "نوش جان — با تشکر از انتخاب شما 🌿";
}

public class ReceiptLine
{
    public string Name { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => UnitPrice * Quantity;
}

/*
 * ──────────────────────────── ESC/POS ENCODER ────────────────────────────
 * Byte-level thermal printer protocol (58/80mm, 203dpi, 48/72 chars/line).
 * Commands: ESC @ init | ESC a n align | ESC E n bold | GS ! n size |
 *           ESC t n codepage | ESC d n feed | GS V 42 n cut | GS v 0 raster
 */
public static class EscPosEncoder
{
    public const byte ESC = 0x1B, GS = 0x1D;

    public static byte[] Init() => new[] { ESC, (byte)'@' };

    public static byte[] Align(byte mode) => new[] { ESC, (byte)'a', mode };              // 0 left,1 center,2 right
    public static byte[] Bold(bool on) => new[] { ESC, (byte)'E', (byte)(on ? 1 : 0) };
    public static byte[] Size(byte w, byte h) => new[] { GS, (byte)'!', (byte)(((w - 1) << 4) | (h - 1)) }; // 1..8
    public static byte[] CodePage(byte n) => new[] { ESC, (byte)'t', n };
    public static byte[] Feed(byte lines) => new[] { ESC, (byte)'d', lines };
    public static byte[] Cut() => new[] { GS, (byte)'V', (byte)'B', (byte)3 };            // partial cut, 3 dots feed
    public static byte[] Text(string s, string encoding = "utf-8") =>
        Encoding.GetEncoding(encoding.NormalizeEncodingName()).GetBytes(s);
    public static byte[] Line(byte[] prefix, string text, string enc) => prefix.Concat(Text(text + "\n", enc)).ToArray();

    /// <summary>GS v 0 raster bit image: m=0, xL xH yL yH, then packed 1bpp rows (1 = black).</summary>
    public static byte[] RasterImage(byte[] bitmap1bpp, int widthPx, int heightPx)
    {
        var header = new List<byte> { GS, (byte)'v', (byte)'0', 0 };
        header.AddRange(BitConverter.GetBytes((ushort)widthPx));
        header.AddRange(BitConverter.GetBytes((ushort)heightPx));
        return header.Concat(bitmap1bpp).ToArray();
    }

    private static string NormalizeEncodingName(this string name) => name.ToLowerInvariant() switch
    {
        "utf-8" or "utf8" => "utf-8",
        "cp1256" or "windows-1256" or "arabic" => "windows-1256",
        "cp437" => "ibm437",
        "cp850" => "ibm850",
        _ => "utf-8"
    };

    /// <summary>Full plain-text receipt (48 cols) → bytes. Persian text should use raster mode instead.</summary>
    public static byte[] EncodeReceipt(ReceiptData r, string encoding = "utf-8")
    {
        var ms = new MemoryStream();
        void W(params byte[][] parts) { foreach (var p in parts) ms.Write(p); }
        var ci = CultureInfo.InvariantCulture;
        W(Init(), Align(1), Bold(true), Size(2, 2), Text(r.RestaurantName + "\n", encoding), Size(1, 1), Bold(false));
        W(Align(0), Text(new string('─', 48) + "\n", encoding));
        W(Align(0), Text($"Branch: {r.BranchName}\n", encoding));
        W(Text($"Order:  {r.OrderNumber}    {r.CompletedAt.ToLocalTime():yyyy-MM-dd HH:mm}\n", encoding));
        if (!string.IsNullOrWhiteSpace(r.Cashier)) W(Text($"Cashier:{r.Cashier}\n", encoding));
        W(Text(new string('─', 48) + "\n", encoding));
        foreach (var l in r.Lines)
            W(Text($"{l.Quantity,2}× {l.Name}".PadRight(30) + $"{l.Total.ToString("N0", ci),18}\n", encoding));
        W(Text(new string('─', 48) + "\n", encoding));
        W(Text($"Subtotal:".PadRight(30) + $"{r.SubTotal.ToString("N0", ci),18}\n", encoding));
        if (r.Discount > 0) W(Text($"Discount:".PadRight(30) + $"-{r.Discount.ToString("N0", ci),17}\n", encoding));
        W(Text($"VAT:".PadRight(30) + $"{r.Tax.ToString("N0", ci),18}\n", encoding));
        W(Bold(true), Text($"TOTAL:".PadRight(30) + $"{r.Total.ToString("N0", ci),18} T\n", encoding), Bold(false));
        W(Align(1), Text($"\nPaid via: {r.PaymentMethod}" + (r.RefId != null ? $"  Ref:{r.RefId}" : "") + "\n", encoding));
        W(Text(r.Footer + "\n\n\n", encoding), Feed(3), Cut());
        return ms.ToArray();
    }

    /// <summary>Kitchen order ticket — big text, item-per-line, no prices.</summary>
    public static byte[] EncodeKitchenTicket(KitchenTicket t, string encoding = "utf-8")
    {
        var ms = new MemoryStream();
        void W(params byte[][] parts) { foreach (var p in parts) ms.Write(p); }
        W(Init(), Align(1), Bold(true), Size(2, 2), Text($"#{t.OrderNumber}\n", encoding), Size(1, 1));
        W(Text($"{t.TableOrType}   {t.CreatedAt:HH:mm}\n", encoding), Bold(false));
        W(Text(new string('=', 48) + "\n", encoding));
        foreach (var l in t.Lines)
        {
            W(Bold(true), Size(2, 2), Text($"{l.Quantity}× {l.Name}\n", encoding), Size(1, 1), Bold(false));
            if (!string.IsNullOrWhiteSpace(l.Notes)) W(Align(2), Text($"» {l.Notes}\n", encoding));
        }
        W(Text(new string('=', 48) + "\n", encoding));
        if (!string.IsNullOrWhiteSpace(t.Customer)) W(Text($"Guest: {t.Customer}\n", encoding));
        if (!string.IsNullOrWhiteSpace(t.Note)) W(Text($"Note: {t.Note}\n", encoding));
        W(Feed(4), Cut());
        return ms.ToArray();
    }
}
