#!/usr/bin/env python3
"""Generate narration audio per scene via z-ai CLI TTS, measure durations."""
import json, subprocess, os, sys

SCENES = [
    ("Zafaran Restaurant Operating System. One platform for dine-in, online ordering, and every role in your restaurant — built on dot NET.", 6.0),
    ("The architecture: an ASP NET Core server with Signal R, EF Core and SQLite, a beautiful Windows desktop app, a customer website, and mobile-ready JWT APIs.", 6.0),
    ("Every role gets its own dashboard. Super Admin, Manager, Cashier, Waiter, Kitchen, Accountant, and Inventory — one sign-in, instant access.", 6.0),
    ("The Manager's command center shows live revenue, open orders, tables, and low-stock alerts, with a seven-day sales chart updated in real time.", 6.0),
    ("In the Kitchen Display System, tickets arrive the moment an order is confirmed. Chefs bump items through queued, cooking, and ready — with live timers and sound alerts.", 6.0),
    ("Waiters work from a live floor plan. Tap a table, take the order, and send it straight to the kitchen.", 6.0),
    ("At the cashier, payments flow through Zarinpal, Zibal, I D Pay, and all major Iranian bank gateways — with a built-in sandbox for safe testing.", 6.0),
    ("Here's the magic: every dish has a recipe. When an order completes, the system automatically deducts each ingredient from stock — and warns you before you run out.", 6.0),
    ("Accounting posts double-entry journals for every sale. Profit and loss, expenses, and X reports are always one click away.", 6.0),
    ("Unlimited cameras stream live into the dashboard. And with E S C POS thermal printing, kitchen tickets and receipts print in Persian — pixel perfect.", 6.0),
    ("Zafaran Restaurant OS. Server, Windows app, web, and mobile — one system, every role, real time. Open source on Git Hub.", 6.0),
]

outdir = "/home/z/my-project/video"
os.makedirs(f"{outdir}/audio", exist_ok=True)
durations = []
for i, (text, minlen) in enumerate(SCENES):
    wav = f"{outdir}/audio/voice_{i:02d}.wav"
    if not os.path.exists(wav) or os.path.getsize(wav) < 1000:
        r = subprocess.run(["z-ai", "tts", "-i", text, "-o", wav, "--voice", "jam", "--speed", "1.0", "-f", "wav"],
                           capture_output=True, text=True, timeout=120)
        if not os.path.exists(wav) or os.path.getsize(wav) < 1000:
            print(f"scene {i} TTS FAILED: {r.stdout} {r.stderr}", file=sys.stderr)
            subprocess.run(["ffmpeg", "-y", "-f", "lavfi", "-i", "anullsrc=r=24000:cl=mono", "-t", str(minlen), wav])
    padded = f"{outdir}/audio/scene_{i:02d}.wav"
    subprocess.run(["ffmpeg", "-y", "-i", wav, "-af", "apad=pad_dur=1.2", padded], capture_output=True)
    dur = float(subprocess.run(["ffprobe", "-v", "quiet", "-show_entries", "format=duration",
                                "-of", "csv=p=0", padded], capture_output=True, text=True).stdout.strip() or minlen)
    durations.append(max(dur, minlen))
    print(f"scene {i}: {dur:.1f}s")

json.dump(durations, open(f"{outdir}/durations.json", "w"))
print("durations:", [round(d, 1) for d in durations])
