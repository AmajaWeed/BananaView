"""Generates small format-badge BMPs for the installer's file-association
picker (TNewCheckListBox with per-item icons). Not part of the app itself -
build-time only, referenced from BananaView.iss's [Files] (Flags: dontcopy)."""
import os
from PIL import Image, ImageDraw, ImageFont

OUT_DIR = os.path.join(os.path.dirname(__file__), "icons")
SIZE = 24

# (filename, label, fill_color)
BADGES = [
    ("png.bmp", "PNG", (0x3A, 0x7B, 0xD9)),
    ("jpg.bmp", "JPG", (0x3A, 0x7B, 0xD9)),
    ("jfif.bmp", "JFIF", (0x3A, 0x7B, 0xD9)),
    ("bmp.bmp", "BMP", (0x3A, 0x7B, 0xD9)),
    ("tif.bmp", "TIF", (0x5C, 0x8A, 0xC8)),
    ("gif.bmp", "GIF", (0xE0, 0x8E, 0x2A)),
    ("webp.bmp", "WEBP", (0x4C, 0xAF, 0x50)),
    ("ico.bmp", "ICO", (0x90, 0x90, 0x90)),
    ("icns.bmp", "ICNS", (0x9C, 0x5C, 0xD9)),
    ("psd.bmp", "PSD", (0x2E, 0x5A, 0xA8)),
    ("procreate.bmp", "PRC", (0xE0, 0x4F, 0x8C)),
    ("sai2.bmp", "SAI2", (0x2A, 0xAE, 0xA8)),
    ("kra.bmp", "KRA", (0xF4, 0x4A, 0x36)),
    ("clip.bmp", "CLIP", (0x2F, 0x2F, 0x8F)),
    ("avif.bmp", "AVIF", (0x4C, 0xAF, 0x50)),
]

os.makedirs(OUT_DIR, exist_ok=True)

try:
    font = ImageFont.truetype("segoeuib.ttf", 8)
except Exception:
    font = ImageFont.load_default()

for filename, label, color in BADGES:
    img = Image.new("RGB", (SIZE, SIZE), (0x2A, 0x2A, 0x2A))  # matches the app's dark chrome as a mask-free background
    draw = ImageDraw.Draw(img)
    pad = 1
    draw.rounded_rectangle([pad, pad, SIZE - 1 - pad, SIZE - 1 - pad], radius=5, fill=color)

    bbox = draw.textbbox((0, 0), label, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    tx = (SIZE - tw) / 2 - bbox[0]
    ty = (SIZE - th) / 2 - bbox[1]
    draw.text((tx, ty), label, font=font, fill=(255, 255, 255))

    img.save(os.path.join(OUT_DIR, filename), "BMP")

print(f"Wrote {len(BADGES)} badges to {OUT_DIR}")
