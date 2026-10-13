"""Рисует иконку ZI-scope (src/LumeneraViewer/Assets/ZI-scope.ico и превью PNG).
Запуск: python tools/make_icon.py  (нужен Pillow). Объектив микроскопа сверху:
линза с градиентом синий → фиолетовый → розовый (палитра Gemini) на тёмной плитке."""
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

S = 1024
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "LumeneraViewer", "Assets")

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))

def sweep_gradient(size, stops):
    """Конический градиент по кругу (stops — цвета по углу, замыкается)."""
    img = Image.new("RGB", (size, size))
    px = img.load(); c = size / 2; n = len(stops)
    for y in range(size):
        for x in range(size):
            a = (math.atan2(y - c, x - c) + math.pi * 0.75) / (2 * math.pi) % 1.0
            f = a * n; i = int(f) % n
            px[x, y] = lerp(stops[i], stops[(i + 1) % n], f - int(f))
    return img

def circle_mask(size, r_out, r_in=0, blur=0):
    m = Image.new("L", (size, size), 0); d = ImageDraw.Draw(m); c = size / 2
    d.ellipse((c - r_out, c - r_out, c + r_out, c + r_out), fill=255)
    if r_in: d.ellipse((c - r_in, c - r_in, c + r_in, c + r_in), fill=0)
    return m.filter(ImageFilter.GaussianBlur(blur)) if blur else m

def build():
    # Плитка: тёмная, со скруглением, лёгкий вертикальный градиент
    tile = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    bg = Image.new("RGB", (S, S))
    dbg = ImageDraw.Draw(bg)
    for y in range(S):
        dbg.line([(0, y), (S, y)], fill=lerp((0x2A, 0x2C, 0x30), (0x13, 0x13, 0x14), y / S))
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).rounded_rectangle((40, 40, S - 40, S - 40), radius=210, fill=255)
    tile.paste(bg, (0, 0), m)

    stops = [(0x42, 0x85, 0xF4), (0x9B, 0x72, 0xCB), (0xD9, 0x65, 0x70), (0x9B, 0x72, 0xCB)]
    grad = sweep_gradient(S, stops)

    # Свечение под кольцом
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    glow.paste(grad, (0, 0), circle_mask(S, 380, 250, blur=40).point(lambda v: v * 0.55))
    tile = Image.alpha_composite(tile, glow)

    # Внешнее кольцо оправы объектива
    ring = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ring.paste(grad, (0, 0), circle_mask(S, 350, 268))
    tile = Image.alpha_composite(tile, ring)

    # Тонкое внутреннее кольцо
    inner = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    inner.paste((0xA8, 0xC7, 0xFA), (0, 0), circle_mask(S, 232, 214).point(lambda v: v * 0.6))
    tile = Image.alpha_composite(tile, inner)

    # Линза: тёмно-синее стекло с радиальным градиентом
    lens = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    lg = Image.new("RGB", (S, S)); px = lg.load(); c = S / 2
    for y in range(int(c - 200), int(c + 200)):
        for x in range(int(c - 200), int(c + 200)):
            t = min(1.0, math.hypot(x - c + 50, y - c + 60) / 260)
            px[x, y] = lerp((0x3A, 0x5F, 0xB8), (0x0B, 0x12, 0x2A), t)
    lens.paste(lg, (0, 0), circle_mask(S, 196))
    tile = Image.alpha_composite(tile, lens)

    # Блики на стекле
    hl = Image.new("L", (S, S), 0); d = ImageDraw.Draw(hl)
    d.ellipse((c - 130, c - 140, c - 20, c - 50), fill=230)
    d.ellipse((c + 60, c + 70, c + 100, c + 110), fill=150)
    hl = hl.filter(ImageFilter.GaussianBlur(10))
    white = Image.new("RGBA", (S, S), (255, 255, 255, 0)); white.putalpha(hl)
    tile = Image.alpha_composite(tile, white)
    return tile

def main():
    os.makedirs(OUT, exist_ok=True)
    big = build()
    sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
    big.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "ZI-scope.ico"), sizes=[(s, s) for s in sizes])
    big.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "ZI-scope-256.png"))
    print("ok", OUT)

if __name__ == "__main__":
    main()
