"""生成 iOS 用的 1024x1024 应用图标（不带透明通道）。"""
import os
import sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import make_icon as m

OUT = os.path.join(os.path.dirname(HERE), "ios", "Sources", "Assets.xcassets", "AppIcon.appiconset")
os.makedirs(OUT, exist_ok=True)

base = m.make_base(1024)
note = m.make_note(1024)
art = m.center_on(1024, base, note)

# iOS 图标不能有 alpha，压到深色底上
canvas = Image.new("RGB", (1024, 1024), (0x0B, 0x10, 0x15))
canvas.paste(art, (0, 0), art)

path = os.path.join(OUT, "icon-1024.png")
canvas.save(path, "PNG")
print("written:", path, os.path.getsize(path), "bytes")
