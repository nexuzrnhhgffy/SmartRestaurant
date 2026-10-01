namespace SmartRestaurant.Domain.Common;

/// <summary>
/// فارسی‌سازی — Persian (Farsi) localization helpers:
/// Persian digits, Toman money formatting, Jalali (Shamsi) calendar conversion
/// and Persian labels for every domain enum. Used by server responses,
/// receipts, reports and shared with the WPF client.
/// </summary>
public static class Fa
{
    private const string Digits = "۰۱۲۳۴۵۶۷۸۹";
    private static readonly string[] FaMonths = { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
                                                  "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
    private static readonly string[] FaWeekdays = { "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه" };

    /// <summary>Convert Latin digits inside any string to Persian digits.</summary>
    public static string DigitsToFa(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        var chars = s.Select(c => c is >= '0' and <= '9' ? Digits[c - '0'] : c).ToArray();
        return new string(chars);
    }

    /// <summary>Number with thousand separators + Persian digits, e.g. 385000 → «۳۸۵٬۰۰۰».</summary>
    public static string Num(long v) => DigitsToFa(v.ToString("N0").Replace(",", "٬"));
    public static string Num(int v) => Num((long)v);
    public static string Num(decimal v) => DigitsToFa(Math.Round(v, 0).ToString("N0").Replace(",", "٬"));

    /// <summary>Money in Toman, e.g. «۳۸۵٬۰۰۰ تومان».</summary>
    public static string Money(decimal v) => Num(v) + " تومان";
    public static string Money(double v) => Money((decimal)v);

    /// <summary>Gregorian → Jalali (Shamsi) date as «۱۴۰۴/۰۷/۰۹».</summary>
    public static string Jalali(DateTime g)
    {
        var (jy, jm, jd) = ToJalali(g);
        return DigitsToFa($"{jy:0000}/{jm:00}/{jd:00}");
    }

    /// <summary>Long Jalali date, e.g. «پنجشنبه ۹ مهر ۱۴۰۴».</summary>
    public static string JalaliLong(DateTime g)
    {
        var (jy, jm, jd) = ToJalali(g);
        return $"{FaWeekdays[(int)g.DayOfWeek]} {DigitsToFa(jd.ToString())} {FaMonths[jm - 1]} {DigitsToFa(jy.ToString())}";
    }

    /// <summary>Short Jalali date + time «۱۴۰۴/۰۷/۰۹ ۱۲:۳۴».</summary>
    public static string JalaliTime(DateTime g) => Jalali(g) + " " + DigitsToFa(g.ToString("HH:mm"));

    /// <summary>Persian clock «۱۲:۳۴:۵۶».</summary>
    public static string Clock(DateTime g) => DigitsToFa(g.ToString("HH:mm:ss"));

    /// <summary>Persian time «۱۲:۳۴».</summary>
    public static string Time(DateTime g) => DigitsToFa(g.ToString("HH:mm"));

    /// <summary>Persian duration «۲ ساعت و ۱۵ دقیقه» / «۱۵ دقیقه».</summary>
    public static string Duration(int minutes) => minutes >= 60
        ? $"{Num(minutes / 60)} ساعت و {Num(minutes % 60)} دقیقه"
        : $"{Num(minutes)} دقیقه";

    // ─── Jalali conversion (standard Borkowski algorithm, accurate 1178–1633 AP) ───
    public static (int y, int m, int d) ToJalali(DateTime g)
    {
        int gy = g.Year, gm = g.Month, gd = g.Day;
        int[] gDaysInMonth = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        long gDayNo = 365L * (gy - 1);
        for (int i = 1; i < gm; i++) gDayNo += gDaysInMonth[i];
        gDayNo += gd - 1;
        if (gm <= 2) gy--;
        else if (IsGregLeap(gy)) gDayNo++; // after Feb
        gDayNo -= 79;                       // days since Jalali epoch start
        long jNp = gDayNo / 12053;
        gDayNo %= 12053;
        long jy2 = 979 + 33 * jNp + 4 * (gDayNo / 1461);
        gDayNo %= 1461;
        if (gDayNo >= 366) { jy2 += (gDayNo - 1) / 365; gDayNo = (gDayNo - 1) % 365; }
        int jy = (int)jy2;
        int jm = gDayNo < 186 ? (int)(gDayNo / 31) + 1 : (int)((gDayNo - 6) / 30) + 1;
        int jd = (int)(gDayNo < 186 ? (gDayNo % 31) + 1 : ((gDayNo - 6) % 30) + 1);
        return (jy, jm, jd);
    }
    private static bool IsGregLeap(int y) => y % 4 == 0 && (y % 100 != 0 || y % 400 == 0);

    // ─── Persian labels for enums (clients + receipts use these) ───
    public static string Label(OrderStatus s) => s switch
    {
        OrderStatus.Pending => "در انتظار تأیید", OrderStatus.Confirmed => "تأیید شده",
        OrderStatus.Preparing => "در حال آماده‌سازی", OrderStatus.Ready => "آماده",
        OrderStatus.Served => "سرو شده", OrderStatus.Completed => "تکمیل شده",
        OrderStatus.Cancelled => "لغو شده", _ => s.ToString()
    };

    public static string Label(OrderType t) => t switch
    {
        OrderType.DineIn => "حضوری", OrderType.Takeaway => "بیرون‌بر",
        OrderType.Delivery => "پیک", _ => t.ToString()
    };

    public static string Label(PaymentStatus s) => s switch
    {
        PaymentStatus.Unpaid => "پرداخت نشده", PaymentStatus.Pending => "در انتظار پرداخت",
        PaymentStatus.Paid => "پرداخت شده", PaymentStatus.Failed => "ناموفق",
        PaymentStatus.Refunded => "بازگشت داده شده", _ => s.ToString()
    };

    public static string Label(TableStatus s) => s switch
    {
        TableStatus.Free => "خالی", TableStatus.Occupied => "اشغال",
        TableStatus.Reserved => "رزرو شده", TableStatus.OutOfService => "خارج از سرویس",
        _ => s.ToString()
    };

    public static string Label(ReservationStatus s) => s switch
    {
        ReservationStatus.Pending => "در انتظار تأیید", ReservationStatus.Confirmed => "تأیید شده",
        ReservationStatus.Seated => "نشسته", ReservationStatus.Cancelled => "لغو شده",
        ReservationStatus.NoShow => "عدم حضور", _ => s.ToString()
    };

    public static string Label(UserRole r) => r switch
    {
        UserRole.SuperAdmin => "مدیر ارشد سیستم", UserRole.Manager => "مدیر رستوران",
        UserRole.Cashier => "صندوقدار", UserRole.Waiter => "گارسون",
        UserRole.Kitchen => "آشپزخانه", UserRole.Accountant => "حسابدار",
        UserRole.Customer => "مشتری", UserRole.Inventory => "انبار", _ => r.ToString()
    };

    public static string Label(OrderItemStatus s) => s switch
    {
        OrderItemStatus.Queued => "در صف", OrderItemStatus.Preparing => "در حال پخت",
        OrderItemStatus.Ready => "آماده", OrderItemStatus.Delivered => "تحویل شده",
        OrderItemStatus.Cancelled => "لغو شده", _ => s.ToString()
    };

    public static string Label(PaymentGatewayType g) => g switch
    {
        PaymentGatewayType.Cash => "نقدی", PaymentGatewayType.CardPresent => "کارتخوان حضوری",
        PaymentGatewayType.Zarinpal => "زرین‌پال", PaymentGatewayType.Zibal => "زیبال",
        PaymentGatewayType.IDPay => "آی‌دی‌پی", PaymentGatewayType.PayIr => "پی‌آی‌آر",
        PaymentGatewayType.NextPay => "نکست‌پی", PaymentGatewayType.Mellat => "بانک ملت",
        PaymentGatewayType.Saman => "بانک سامان", PaymentGatewayType.Parsian => "بانک پارسیان",
        PaymentGatewayType.Pasargad => "بانک پاسارگاد", PaymentGatewayType.Novin => "بانک نوین",
        _ => g.ToString()
    };

    /// <summary>Map English status strings (from API) → Persian; unknown → original.</summary>
    public static string LabelOf(string? english) => english switch
    {
        "Pending" => "در انتظار تأیید", "Confirmed" => "تأیید شده", "Preparing" => "در حال آماده‌سازی",
        "Ready" => "آماده", "Served" => "سرو شده", "Completed" => "تکمیل شده", "Cancelled" => "لغو شده",
        "Queued" => "در صف", "Delivered" => "تحویل شده",
        "Unpaid" => "پرداخت نشده", "Paid" => "پرداخت شده", "Failed" => "ناموفق", "Refunded" => "بازگشت وجه",
        "PartiallyPaid" => "پرداخت جزئی",
        "Free" => "خالی", "Occupied" => "اشغال", "Reserved" => "رزرو شده", "OutOfService" => "خارج از سرویس",
        "DineIn" => "حضوری", "Takeaway" => "بیرون‌بر", "Delivery" => "پیک",
        "Available" => "فعال", "Dirty" => "نیاز به تمیزکاری",
        "Approved" => "تأیید شده", "Rejected" => "رد شده", "Scheduled" => "برنامه‌ریزی شده",
        "InProgress" => "در جریان", "Absent" => "غایب",
        "Inquiry" => "استعلام", "Quoted" => "ارائه قیمت", "Seated" => "نشسته", "NoShow" => "عدم حضور",
        "Draft" => "پیش‌نویس", "Ordered" => "سفارش داده شده", "Received" => "دریافت شده",
        _ => english ?? ""
    };
}
