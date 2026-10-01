#!/usr/bin/env python3
"""Screenshot each 1920x1080 scene section via Playwright."""
import json, pathlib
from playwright.sync_api import sync_playwright

out = pathlib.Path("/home/z/my-project/video/frames")
out.mkdir(parents=True, exist_ok=True)
n = len(json.load(open("/home/z/my-project/video/durations.json")))

with sync_playwright() as p:
    browser = p.chromium.launch()
    page = browser.new_page(viewport={"width": 1920, "height": 1080}, device_scale_factor=1)
    page.goto("file:///home/z/my-project/video/scenes/scenes.html")
    page.wait_for_timeout(700)
    for i in range(n):
        el = page.locator(f"#s{i}")
        el.screenshot(path=str(out / f"scene_{i:02d}.png"))
        print(f"scene_{i:02d}.png")
    browser.close()
print("done")
