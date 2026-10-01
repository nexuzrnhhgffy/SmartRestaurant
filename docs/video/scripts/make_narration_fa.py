#!/usr/bin/env python3
"""روایت فارسی — Persian narration via edge-tts (fa-IR neural voices), measure durations."""
import json, subprocess, os, sys, asyncio
import edge_tts

VOICE = "fa-IR-DilaraNeural"

SCENES = [
    ("رستوران هوشمند زعفران؛ یک پلتفرم واحد برای سرو حضوری، سفارش آنلاین و همه نقش‌های رستوران شما. کاملاً فارسی، ساخته‌شده بر بستر دات‌نت.", 8.0),
    ("معماری سیستم: یک سرور ای‌اس‌پی‌دات‌نت‌کور با سیگنال‌آر و دیتابیس، یک اپلیکیشن ویندوزی زیبا، وب‌سایت مشتریان و اِی‌پی‌آی آماده موبایل با احراز هویت جی‌دبلیو‌تی.", 8.5),
    ("هر نقش، داشبورد اختصاصی خودش را دارد؛ مدیر ارشد، مدیر، صندوقدار، گارسون، آشپزخانه، حسابدار و انبار. یک ورود، دسترسی فوری.", 8.0),
    ("مرکز فرماندهی مدیر، درآمد زنده، سفارش‌های باز، وضعیت میزها و هشدار کمبود موجودی را یکجا نشان می‌دهد؛ با اعداد کاملاً فارسی و تقویم شمسی.", 8.5),
    ("در نمایشگر آشپزخانه، تیکت‌ها لحظه‌ای که سفارش تأیید می‌شود می‌رسند. سرآشپز آیتم‌ها را از صف تا پخت و آماده جلو می‌برد؛ با تایمر زنده و هشدار صوتی.", 9.0),
    ("گارسون‌ها از روی نقشه زنده سالن کار می‌کنند. روی میز کلیک کنید، سفارش بگیرید و مستقیم به آشپزخانه بفرستید.", 6.5),
    ("در صندوق، پرداخت‌ها از زرین‌پال، زیبال و همه درگاه‌های بانکی ایرانی عبور می‌کنند؛ با محیط آزمایشی امن برای تست بدون پول واقعی.", 8.5),
    ("و اما جادوی اصلی: هر غذا یک رسپی دارد. با تکمیل هر سفارش، مواد اولیه به‌صورت خودکار از انبار کسر می‌شود و پیش از تمام‌شدن، هشدار می‌دهید.", 9.0),
    ("حسابداری برای هر فروش، سند دوطرفه صادر می‌کند. سود و زیان، هزینه‌ها و گزارش صندوق، همیشه یک کلیک فاصله دارند.", 7.5),
    ("دوربین‌های نامحدود، به‌صورت زنده داخل داشبورد پخش می‌شوند؛ و با چاپ حرارتی ای‌اس‌سی‌پاز، تیکت آشپزخانه و رسید مشتری به فارسی چاپ می‌شود.", 8.5),
    ("زعفران؛ سیستم‌عامل رستوران شما. سرور، اپلیکیشن ویندوز، وب و موبایل. یک سیستم، همه نقش‌ها، کاملاً لحظه‌ای. همین حالا روی گیت‌هاب.", 8.5),
]

outdir = "/home/z/my-project/video"
os.makedirs(f"{outdir}/audio", exist_ok=True)

async def synth(text, mp3):
    tts = edge_tts.Communicate(text, VOICE, rate="+6%")
    await tts.save(mp3)

durations = []
for i, (text, minlen) in enumerate(SCENES):
    mp3 = f"{outdir}/audio/voice_{i:02d}.mp3"
    if not os.path.exists(mp3) or os.path.getsize(mp3) < 2000:
        try:
            asyncio.run(synth(text, mp3))
        except Exception as e:
            print(f"scene {i} TTS FAILED: {e}", file=sys.stderr)
            subprocess.run(["ffmpeg", "-y", "-f", "lavfi", "-i", "anullsrc=r=24000:cl=mono", "-t", str(minlen), mp3])
    padded = f"{outdir}/audio/scene_{i:02d}.wav"
    subprocess.run(["ffmpeg", "-y", "-i", mp3, "-af", "apad=pad_dur=0.9", padded], capture_output=True)
    dur = float(subprocess.run(["ffprobe", "-v", "quiet", "-show_entries", "format=duration",
                                "-of", "csv=p=0", padded], capture_output=True, text=True).stdout.strip() or minlen)
    durations.append(max(dur, minlen))
    print(f"scene {i}: {dur:.1f}s")

json.dump(durations, open(f"{outdir}/durations.json", "w"))
print("durations:", [round(d, 1) for d in durations])
