# 🎬 ویدیوی معرفی محصول — رستوران هوشمند زعفران
# Product Illustration Video — Zafaran Smart Restaurant OS

> **فایل ویدیو:** [`SmartRestaurant_Product_Video.mp4`](./SmartRestaurant_Product_Video.mp4)
> Video file: [`SmartRestaurant_Product_Video.mp4`](./SmartRestaurant_Product_Video.mp4)

## مشخصات فنی / Technical Specs

| مشخصه | مقدار |
|---|---|
| مدت زمان / Duration | ۱۱۹.۸ ثانیه (~۲ دقیقه) |
| رزولوشن / Resolution | 1920×1080 (Full HD) |
| کدک تصویر / Video codec | H.264 (yuv420p) |
| صدا / Audio | AAC — گویندهٔ فارسی «دلارا» (fa-IR-DilaraNeural) |
| حجم / Size | ~۱۴.۴ مگابایت |
| زبان / Language | فارسی (RTL) با زیرنویس‌های روی صحنه‌ها |

## ساختار صحنه‌ها / Scene Breakdown (11 scenes)

1. **خوش‌آمد / عنوان برند** — معرفی «سیستم‌عامل رستوران زعفران»
2. **سفارش آنلاین مشتری** — سایت سفارش آنلاین RTL، سبد خرید، پرداخت زرین‌پال
3. **نقشهٔ سالن و سفارش حضوری** — مدیریت میزها توسط گارسون
4. **مانیتور آشپزخانه (KDS)** — دریافت زندهٔ سفارش‌ها با SignalR
5. **پرداخت و صندوق** — درگاه‌های بانکی ایرانی، سند حسابداری خودکار
6. **کسر خودکار موجودی** — نقشهٔ دستور پخت ← کسر مواد اولیه پس از اتمام سفارش
7. **حسابداری** — دفتر روزنامه دوطرفه، سود و زیان
8. **مدیریت شعب** — چند شعبه، گزارش مقایسه‌ای
9. **دوربین‌های نظارتی** — پخش زندهٔ MJPEG روی داشبورد مدیر
10. **اپلیکیشن ویندوز** — کلاینت WPF چندنقشی متصل به همان سرور JWT
11. **جمع‌بندی** — معماری فناوری و اطلاعات تماس

## بازتولید ویدیو / Reproducing the Video

پیش‌نیازها: Python 3، `playwright`، `edge-tts`، `ffmpeg`

```bash
cd docs/video/scripts
python render_scenes.py        # رندر فریم‌های صحنه‌ها با Playwright (HTML → PNG)
python make_narration_fa.py    # تولید گویندگی فارسی با edge-tts
python build_video.py          # ساخت کلیپ‌ها (zoompan + fade) و ادغام نهایی با ffmpeg
```

خروجی نهایی در `download/SmartRestaurant_Product_Video.mp4` قرار می‌گیرد.
Final output lands in `download/SmartRestaurant_Product_Video.mp4`.

## پخش / Playback

```bash
# Linux / macOS
xdg-open docs/video/SmartRestaurant_Product_Video.mp4
# Windows
start docs/video\SmartRestaurant_Product_Video.mp4
```
