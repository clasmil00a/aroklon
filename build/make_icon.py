#!/usr/bin/env python3
"""Generate Aroklon application icons (PNG + multi-size ICO)."""
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), "..", "build")
os.makedirs(OUT, exist_ok=True)

S = 1024
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# rounded-square gradient background
bg = Image.new("RGBA", (S, S), (0, 0, 0, 0))
bd = ImageDraw.Draw(bg)
for y in range(S):
    t = y / (S - 1)
    r = int(99 + (139 - 99) * t)
    g = int(102 + (92 - 102) * t)
    b = int(241 + (246 - 241) * t)
    bd.line([(0, y), (S, y)], fill=(r, g, b, 255))
mask = Image.new("L", (S, S), 0)
md = ImageDraw.Draw(mask)
md.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)
img.paste(bg, (0, 0), mask)

W = (255, 255, 255, 255)
# storefront body
d.rounded_rectangle([250, 430, 774, 800], radius=34, fill=W)
# awning roof
d.rounded_rectangle([206, 300, 818, 430], radius=30, fill=W)
# scallops
for cx in (302, 454, 606, 758):
    d.ellipse([cx - 76, 386, cx + 76, 500], fill=W)
# door
d.rounded_rectangle([430, 590, 594, 800], radius=26, fill=(99, 102, 241, 255))
# door handle
d.ellipse([552, 676, 574, 698], fill=W)
# windows
d.rounded_rectangle([292, 500, 404, 620], radius=18, fill=(99, 102, 241, 255))
d.rounded_rectangle([620, 500, 732, 620], radius=18, fill=(99, 102, 241, 255))
# base line
d.rounded_rectangle([250, 800, 774, 830], radius=15, fill=(255, 255, 255, 210))

img.save(os.path.join(OUT, "icon.png"))

sizes = [16, 24, 32, 48, 64, 128, 256]
img.save(
    os.path.join(OUT, "icon.ico"),
    format="ICO",
    sizes=[(s, s) for s in sizes],
)
print("wrote", os.path.join(OUT, "icon.png"), os.path.join(OUT, "icon.ico"))