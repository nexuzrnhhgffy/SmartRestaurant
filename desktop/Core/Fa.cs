namespace SmartRestaurant.Desktop.Core;

/// <summary>
/// فارسی‌سازی — Persian localization helpers for the WPF client:
/// Persian digits, Toman money, Jalali (Shamsi) calendar, Persian enum labels.
/// Mirrors the server-side Fa helper (SmartRestaurant.Domain.Common.Fa).
/// </summary>
public static class Fa
{
    private const string Digits = "۰۱۲۳۴۵۶۷۸۹";
    private static readonly string[] Months = { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
                                                "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
    private static readonly string[] Weekdays = { "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه" };

    public static string DigitsToFa(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        var chars = s.Select(c => c is >= '0' and <= '9' ? Digits[c - '0'] : c).ToArray();
        return new string(chars);
    }

    public static string Num(long v) => DigitsToFa(v.ToString("N0").Replace(",", "٬"));
    public static string Num(int v) => Num((long)v);
    public static string Num(decimal v) => DigitsToFa(Math.Round(v, 0).ToString("N0").Replace(",", "٬"));

    public static string Money(decimal v) => Num(v) + " تومان";
    public static string Money(double v) => Money((decimal)v);

    public static (int y, int m, int d) ToJalali(DateTime g)
    {
        int gy = g.Year, gm = g.Month, gd = g.Day;
        int[] gDaysInMonth = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        long gDayNo = 365L * (gy - 1);
        for (int i = 1; i < gm; i++) gDayNo += gDaysInMonth[i];
        gDayNo += gd - 1;
        if (gm <= 2) gy--;
        else if (IsGregLeap(gy)) gDayNo++;
        gDayNo -= 79;
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

    /// <summary>«۱۴۰۴/۰۷/۰۹»</summary>
    public static string Jalali(DateTime g)
    {
        var (jy, jm, jd) = ToJalali(g);
        return DigitsToFa($"{jy:0000}/{jm:00}/{jd:00}");
    }

    /// <summary>«پنجشنبه ۹ مهر ۱۴۰۴»</summary>
    public static string JalaliLong(DateTime g)
    {
        var (jy, jm, jd) = ToJalali(g);
        return $"{Weekdays[(int)g.DayOfWeek]} {DigitsToFa(jd.ToString())} {Months[jm - 1]} {DigitsToFa(jy.ToString())}";
    }

    /// <summary>«۱۴۰۴/۰۷/۰۹ ۱۲:۳۴»</summary>
    public static string JalaliTime(DateTime g) => Jalali(g) + " " + DigitsToFa(g.ToString("HH:mm"));

    /// <summary>«۱۲:۳۴»</summary>
    public static string Time(DateTime g) => DigitsToFa(g.ToString("HH:mm"));

    /// <summary>«۱۲:۳۴:۵۶»</summary>
    public static string Clock(DateTime g) => DigitsToFa(g.ToString("HH:mm:ss"));

    /// <summary>Persian weekday short name for chart axes (e.g. «پ» «ج»).</summary>
    public static string WeekdayShort(DateTime g) => Weekdays[(int)g.DayOfWeek] switch
    {
        "یکشنبه" => "ی", "دوشنبه" => "د", "سه‌شنبه" => "س", "چهارشنبه" => "چ",
        "پنجشنبه" => "پ", "جمعه" => "ج", _ => "ش"
    };

    // ─── status maps (API sends English names) ───
    public static string Status(string? en) => en switch
    {
        "Pending" => "در انتظار تأیید", "Confirmed" => "تأیید شده", "Preparing" => "در حال آماده‌سازی",
        "Ready" => "آماده", "Served" => "سرو شده", "Completed" => "تکمیل شده", "Cancelled" => "لغو شده",
        "Queued" => "در صف", "Delivered" => "تحویل شده",
        "Unpaid" => "پرداخت نشده", "Paid" => "پرداخت شده", "Failed" => "ناموفق",
        "Refunded" => "بازگشت وجه", "PartiallyPaid" => "پرداخت جزئی", "PendingPayment" => "در انتظار پرداخت",
        "Free" => "خالی", "Occupied" => "اشغال", "Reserved" => "رزرو شده", "OutOfService" => "خارج از سرویس",
        "DineIn" => "حضوری", "Takeaway" => "بیرون‌بر", "Delivery" => "پیک",
        "Approved" => "تأیید شده", "Rejected" => "رد شده", "Scheduled" => "برنامه‌ریزی شده",
        "InProgress" => "در جریان", "Absent" => "غایب", "Inquiry" => "استعلام",
        "Quoted" => "ارائه قیمت", "Seated" => "نشسته", "NoShow" => "عدم حضور",
        "Draft" => "پیش‌نویس", "Ordered" => "سفارش داده شده", "Received" => "دریافت شده",
        _ => en ?? ""
    };

    public static string TypeName(string? en) => en switch
    {
        "DineIn" => "حضوری", "Takeaway" => "بیرون‌بر", "Delivery" => "پیک", _ => en ?? ""
    };

    public static string Gateway(string? en) => en switch
    {
        "Cash" => "نقدی", "CardPresent" => "کارتخوان حضوری",
        "Zarinpal" => "زرین‌پال", "Zibal" => "زیبال", "IDPay" => "آی‌دی‌پی",
        "PayIr" => "پی‌آی‌آر", "NextPay" => "نکست‌پی",
        "Mellat" => "بانک ملت", "Saman" => "بانک سامان", "Parsian" => "بانک پارسیان",
        "Pasargad" => "بانک پاسارگاد", "Novin" => "بانک نوین", _ => en ?? ""
    };

    public static string Role(string? en) => en switch
    {
        "SuperAdmin" => "مدیر ارشد سیستم", "Manager" => "مدیر رستوران", "Cashier" => "صندوقدار",
        "Waiter" => "گارسون", "Kitchen" => "آشپزخانه", "Chef" => "آشپزخانه",
        "Accountant" => "حسابدار", "Customer" => "مشتری", "Inventory" => "انبار", _ => en ?? ""
    };

    public static string Station(string? en) => en switch
    {
        "Grill" => "ایستگاه کباب", "HotKitchen" => "آشپزخانه گرم", "Cold" => "آشپزخانه سرد",
        "Dessert" => "دسر", "Bar" => "بار", _ => en ?? ""
    };

    public static string Duration(int minutes) => minutes >= 60
        ? $"{Num(minutes / 60)} ساعت و {Num(minutes % 60)} دقیقه"
        : $"{Num(minutes)} دقیقه";
}
