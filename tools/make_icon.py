"""生成应用图标：圆角渐变底 + 白色八分音符，输出多尺寸 ICO。"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(os.path.dirname(HERE), "src", "QingShang", "Assets")
S = 1024
CORNER = 0.225


def make_base(size):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    grad = Image.new("RGBA", (size, size))
    gd = ImageDraw.Draw(grad)
    top = (0x6E, 0xEE, 0xFF)
    mid = (0x2A, 0xC8, 0xDE)
    bottom = (0x0A, 0x66, 0x84)
    for y in range(size):
        t = y / max(1, size - 1)
        if t < 0.5:
            k = t / 0.5
            c = tuple(int(top[i] + (mid[i] - top[i]) * k) for i in range(3))
        else:
            k = (t - 0.5) / 0.5
            c = tuple(int(mid[i] + (bottom[i] - mid[i]) * k) for i in range(3))
        gd.line([(0, y), (size, y)], fill=(c[0], c[1], c[2], 255))

    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gl = ImageDraw.Draw(glow)
    gl.ellipse([-size * 0.28, -size * 0.38, size * 0.72, size * 0.52],
               fill=(255, 255, 255, 52))
    glow = glow.filter(ImageFilter.GaussianBlur(size * 0.07))
    grad = Image.alpha_composite(grad, glow)

    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([0, 0, size - 1, size - 1], radius=int(size * CORNER), fill=255)
    grad.putalpha(mask)

    edge = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ed = ImageDraw.Draw(edge)
    ed.rounded_rectangle([size * 0.010, size * 0.010, size * 0.990, size * 0.990],
                         radius=int(size * CORNER * 0.97),
                         outline=(255, 255, 255, 70), width=max(2, int(size * 0.0055)))
    grad = Image.alpha_composite(grad, edge)

    return Image.alpha_composite(img, grad)


def rotated_ellipse(cx, cy, rx, ry, angle_deg, steps=128):
    a = math.radians(angle_deg)
    pts = []
    for i in range(steps):
        t = 2 * math.pi * i / steps
        x = rx * math.cos(t)
        y = ry * math.sin(t)
        pts.append((cx + x * math.cos(a) - y * math.sin(a),
                    cy + x * math.sin(a) + y * math.cos(a)))
    return pts


def bezier(p0, p1, p2, steps=64):
    pts = []
    for i in range(steps + 1):
        t = i / steps
        u = 1 - t
        x = u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0]
        y = u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1]
        pts.append((x, y))
    return pts


def thick_line(d, pts, width):
    r = width / 2
    for (x, y) in pts:
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, 255))


def make_note(size):
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    # 符头
    hx, hy = size * 0.318, size * 0.722
    rx, ry = size * 0.152, size * 0.114
    d.polygon(rotated_ellipse(hx, hy, rx, ry, -24), fill=(255, 255, 255, 255))

    # 符干（从符头右上穿到顶部）
    stem_x = hx + rx * 0.62
    stem_w = size * 0.050
    top = size * 0.190
    d.rounded_rectangle([stem_x - stem_w / 2, top, stem_x + stem_w / 2, hy + size * 0.012],
                        radius=stem_w / 2, fill=(255, 255, 255, 255))

    # 两条符尾：从符干顶端沿贝塞尔向右下甩
    tail_w = size * 0.056
    p0 = (stem_x, top + size * 0.010)
    thick_line(d, bezier(p0, (stem_x + size * 0.170, top + size * 0.030),
                         (stem_x + size * 0.300, top + size * 0.250)), tail_w)

    p0b = (stem_x, top + size * 0.128)
    thick_line(d, bezier(p0b, (stem_x + size * 0.150, top + size * 0.150),
                         (stem_x + size * 0.262, top + size * 0.352)), tail_w * 0.94)

    # 抗锯齿
    layer = layer.filter(ImageFilter.GaussianBlur(size * 0.0032))
    return layer


def center_on(size, art, layer):
    bbox = layer.getbbox()
    if not bbox:
        return art
    cx = (bbox[0] + bbox[2]) / 2
    cy = (bbox[1] + bbox[3]) / 2
    dx = size / 2 - cx
    dy = size / 2 - cy + size * 0.012
    shifted = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shifted.paste(layer, (int(round(dx)), int(round(dy))))
    return Image.alpha_composite(art, shifted)


def build():
    os.makedirs(OUT_DIR, exist_ok=True)
    base = make_base(S)
    note = make_note(S)
    art = center_on(S, base, note)

    png_path = os.path.join(OUT_DIR, "app.png")
    art.resize((512, 512), Image.LANCZOS).save(png_path)

    sizes = [256, 128, 96, 64, 48, 40, 32, 24, 16]
    frames = [art.resize((s, s), Image.LANCZOS) for s in sizes]
    ico_path = os.path.join(OUT_DIR, "app.ico")
    frames[0].save(ico_path, format="ICO", sizes=[(s, s) for s in sizes],
                   append_images=frames[1:])
    print("icon:", ico_path, os.path.getsize(ico_path), "bytes")
    print("png :", png_path)


if __name__ == "__main__":
    build()
