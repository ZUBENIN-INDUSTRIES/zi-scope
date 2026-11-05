"""Рисует иконку ZI-scope (src/LumeneraViewer/Assets/ZI-scope.ico и превью ZI-scope-256.png).
Запуск: python tools/make_icon.py  (нужен Pillow и шрифт Segoe UI Bold из Windows).
Сине-серая плитка с аббревиатурой ZIS и тонкой линией-«линейкой» масштаба под ней."""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

S = 1024
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "LumeneraViewer", "Assets")
FONT = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "segoeuib.ttf")

TOP = (0x5B, 0x6F, 0x8A)      # сине-серый, светлее
BOTTOM = (0x26, 0x31, 0x40)   # сине-серый, тёмный
TEXT = (0xEE, 0xF2, 0xF7)
ACCENT = (0x9F, 0xC0, 0xE8)   # светло-голубая линейка


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def build(size=S):
    k = size / S
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    # Плитка: вертикальный градиент, скругление, тонкая светлая кромка сверху
    grad = Image.new("RGB", (size, size))
    d = ImageDraw.Draw(grad)
    for y in range(size):
        d.line([(0, y), (size, y)], fill=lerp(TOP, BOTTOM, y / size))
    pad, radius = int(40 * k), int(210 * k)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((pad, pad, size - pad, size - pad), radius=radius, fill=255)
    img.paste(grad, (0, 0), mask)
    edge = Image.new("L", (size, size), 0)
    ImageDraw.Draw(edge).rounded_rectangle((pad, pad, size - pad, size - pad), radius=radius, outline=90, width=max(1, int(6 * k)))
    img = Image.alpha_composite(img, Image.merge("RGBA", [Image.new("L", (size, size), 255)] * 3 + [edge]))

    # ZIS
    font = ImageFont.truetype(FONT, int(380 * k))
    draw = ImageDraw.Draw(img)
    text = "ZIS"
    l, t, r, b = draw.textbbox((0, 0), text, font=font)
    x = (size - (r - l)) / 2 - l
    y = size * 0.46 - (b - t) / 2 - t
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).text((x, y + 10 * k), text, font=font, fill=(10, 16, 24, 150))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(12 * k)))
    ImageDraw.Draw(img).text((x, y), text, font=font, fill=TEXT)

    # Линейка масштаба под буквами: полоса с засечками на концах
    draw = ImageDraw.Draw(img)
    bar_y = int(size * 0.74)
    x0, x1 = int(size * 0.27), int(size * 0.73)
    h, w = int(18 * k), int(16 * k)
    draw.rounded_rectangle((x0, bar_y - h // 2, x1, bar_y + h // 2), radius=h // 2, fill=ACCENT)
    for xx in (x0, x1 - w):
        draw.rounded_rectangle((xx, bar_y - int(52 * k), xx + w, bar_y + int(52 * k)), radius=w // 2, fill=ACCENT)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    big = build()
    preview = big.resize((256, 256), Image.LANCZOS)
    preview.save(os.path.join(OUT, "ZI-scope-256.png"))
    # Маленькие размеры рисуем отдельно — так буквы чётче, чем при уменьшении большой картинки
    frames = [build(s) if s >= 64 else big.resize((s, s), Image.LANCZOS) for s in (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)]
    frames[-1].save(os.path.join(OUT, "ZI-scope.ico"), sizes=[f.size for f in frames], append_images=frames[:-1])
    print("ok", OUT)


if __name__ == "__main__":
    main()
