#!/usr/bin/env python3
"""Assemble final MP4: Ken Burns zoompan per scene + crossfade-free concat + narration mux."""
import json, subprocess, os

V = "/home/z/my-project/video"
OUT = "/home/z/my-project/download/SmartRestaurant_Product_Video.mp4"
durations = json.load(open(f"{V}/durations.json"))
os.makedirs(f"{V}/clips", exist_ok=True)
os.makedirs(os.path.dirname(OUT), exist_ok=True)

clips = []
from concurrent.futures import ThreadPoolExecutor

def build_clip(args):
    i, dur = args
    src = f"{V}/frames/scene_{i:02d}.png"
    clip = f"{V}/clips/clip_{i:02d}.mp4"
    if os.path.exists(clip) and os.path.getsize(clip) > 50000:
        print(f"clip {i}: cached"); return clip
    fps = 24
    frames = int(dur * fps) + 1
    # slow zoom-in (1.0 → 1.06), 1080p, fade in/out
    vf = (f"zoompan=z='1.0+0.00035*on':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':"
          f"d={frames}:s=1920x1080:fps={fps},"
          f"fade=t=in:st=0:d=0.5,fade=t=out:st={dur-0.5:.2f}:d=0.5,format=yuv420p")
    subprocess.run(["ffmpeg", "-y", "-loop", "1", "-i", src, "-t", f"{dur:.3f}",
                    "-vf", vf, "-r", str(fps), "-c:v", "libx264", "-preset", "veryfast",
                    "-crf", "21", clip], check=True, capture_output=True)
    print(f"clip {i}: {dur:.1f}s")
    return clip

with ThreadPoolExecutor(max_workers=2) as ex:
    clips = list(ex.map(build_clip, list(enumerate(durations))))

# concat
with open(f"{V}/concat.txt", "w") as f:
    for c in clips:
        f.write(f"file '{c}'\n")
silent = f"{V}/video_silent.mp4"
subprocess.run(["ffmpeg", "-y", "-f", "concat", "-safe", "0", "-i", f"{V}/concat.txt",
                "-c", "copy", silent], check=True, capture_output=True)

# audio: concat scene narrations to match video length
aud_list = f"{V}/audlist.txt"
with open(aud_list, "w") as f:
    for i in range(len(durations)):
        f.write(f"file '{V}/audio/scene_{i:02d}.wav'\n")
voice = f"{V}/voice_all.wav"
subprocess.run(["ffmpeg", "-y", "-f", "concat", "-safe", "0", "-i", aud_list,
                "-af", "loudnorm=I=-16:TP=-1.5", voice], check=True, capture_output=True)

total_v = float(subprocess.run(["ffprobe", "-v", "quiet", "-show_entries", "format=duration",
                                "-of", "csv=p=0", silent], capture_output=True, text=True).stdout)
total_a = float(subprocess.run(["ffprobe", "-v", "quiet", "-show_entries", "format=duration",
                                "-of", "csv=p=0", voice], capture_output=True, text=True).stdout)
print(f"video {total_v:.1f}s vs audio {total_a:.1f}s")

# mux: pad/trim audio to video length, add gentle fade
subprocess.run(["ffmpeg", "-y", "-i", silent, "-i", voice,
                "-filter_complex",
                f"[1:a]apad=whole_dur={total_v:.2f},atrim=0:{total_v:.2f},afade=t=out:st={total_v-2:.2f}:d=2[a]",
                "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "160k",
                "-movflags", "+faststart", OUT], check=True, capture_output=True)

final = float(subprocess.run(["ffprobe", "-v", "quiet", "-show_entries", "format=duration",
                              "-of", "csv=p=0", OUT], capture_output=True, text=True).stdout)
size = os.path.getsize(OUT) / 1e6
print(f"✅ FINAL: {OUT} — {final:.1f}s, {size:.1f} MB")
