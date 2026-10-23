#!/usr/bin/env python3
"""Cut the AI-painted resource icons (tools/art/ai-icons-src/*.jpg, object on near-white) into
transparent 256px PNGs with a subtle dark outline + soft drop shadow, for every UI use
(Assets/_Project/Resources/Art/Icons/Painted/{Wood,Brick,Wheat,Sheep,Stone,Fish}.png).

Keying: the background is the near-white region connected to the image border (flood fill), so
white inside the object (the sheep's wool, behind its grey outline) is kept."""
import os
from collections import deque

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "tools", "art", "ai-icons-src")
DST = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Icons", "Painted")
NAMES = {"lumber": "Wood", "brick": "Brick", "wheat": "Wheat", "wool": "Sheep", "ore": "Stone", "fish": "Fish"}
OUT = 256
MARGIN = 0.07
T_HARD = 26.0   # distance from background colour that is certainly object
T_SOFT = 8.0    # below this it is certainly background


def flood_background(dist, thresh):
    h, w = dist.shape
    bg = np.zeros((h, w), bool)
    cand = dist < thresh
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if cand[y, x] and not bg[y, x]:
                bg[y, x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if cand[y, x] and not bg[y, x]:
                bg[y, x] = True
                q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and cand[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                q.append((ny, nx))
    return bg


def cut(path):
    im = np.asarray(Image.open(path).convert("RGB"), np.float32)
    border = np.concatenate([im[0], im[-1], im[:, 0], im[:, -1]])
    bgc = np.median(border, axis=0)
    dist = np.sqrt(((im - bgc) ** 2).sum(-1))
    bg = flood_background(dist, T_HARD)
    # Alpha: 1 inside the object; on the border-connected background ramp by colour distance.
    alpha = np.where(bg, np.clip((dist - T_SOFT) / (T_HARD - T_SOFT), 0, 1), 1.0)
    a_img = Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3))
    a_img = a_img.filter(ImageFilter.GaussianBlur(0.7))
    alpha = np.asarray(a_img, np.float32) / 255.0
    # Un-mix the white background from semi-transparent edge pixels.
    a = np.maximum(alpha, 1e-3)[..., None]
    rgb = np.clip((im - bgc * (1 - a)) / a, 0, 255)
    rgb = np.where(alpha[..., None] > 0.02, rgb, 0)
    rgba = Image.fromarray(np.dstack([rgb, alpha * 255]).astype(np.uint8), "RGBA")
    bbox = Image.fromarray((alpha > 0.1).astype(np.uint8) * 255).getbbox()
    return rgba.crop(bbox)


def finish(obj):
    side = int(max(obj.size) / (1 - 2 * MARGIN))
    big = 4 * OUT
    scale = (big * (1 - 2 * MARGIN)) / max(obj.size)
    obj = obj.resize((max(1, int(obj.width * scale)), max(1, int(obj.height * scale))), Image.LANCZOS)
    canvas = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    ox, oy = (big - obj.width) // 2, (big - obj.height) // 2
    a = obj.getchannel("A")
    full_a = Image.new("L", (big, big), 0)
    full_a.paste(a, (ox, oy))
    # Drop shadow (down-right) and a thin dark outline so icons read on cream UI and badges.
    shadow = full_a.filter(ImageFilter.GaussianBlur(big * 0.018))
    shadow = shadow.transform(shadow.size, Image.AFFINE, (1, 0, -big * 0.012, 0, 1, -big * 0.018))
    shadow = shadow.point(lambda v: int(v * 0.45))
    outline = full_a.filter(ImageFilter.MaxFilter(int(big * 0.012) * 2 + 1)).filter(ImageFilter.GaussianBlur(big * 0.003))
    outline = outline.point(lambda v: int(v * 0.85))
    canvas = Image.alpha_composite(canvas, Image.merge("RGBA", (*[Image.new("L", (big, big), 0)] * 3, shadow)))
    ink = Image.new("RGBA", (big, big), (52, 36, 22, 0))
    ink.putalpha(outline)
    canvas = Image.alpha_composite(canvas, ink)
    layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    layer.paste(obj, (ox, oy))
    canvas = Image.alpha_composite(canvas, layer)
    return canvas.resize((OUT, OUT), Image.LANCZOS)


def main():
    os.makedirs(DST, exist_ok=True)
    for src, name in NAMES.items():
        out = finish(cut(os.path.join(SRC, f"{src}.jpg")))
        out.save(os.path.join(DST, f"{name}.png"), optimize=True)
        print("wrote", name)


if __name__ == "__main__" and os.environ.get("V132", "1") != "1":
    main()


# ---------------------------------------------------------------- v132 additions
SMALL = 96


def tint_ore(img):
    """Ore read as flat grey: grade it to blue-slate stone with cool highlights (luminance gradient map)."""
    arr = np.asarray(img, np.float32)
    rgb, a = arr[..., :3], arr[..., 3:]
    lum = (rgb @ np.array([0.299, 0.587, 0.114], np.float32))[..., None] / 255.0
    stops = np.array([[0.00, 22, 26, 44], [0.30, 52, 66, 98], [0.60, 104, 128, 168],
                      [0.85, 182, 202, 228], [1.00, 238, 244, 252]], np.float32)
    out = np.zeros_like(rgb)
    for c in range(3):
        out[..., c] = np.interp(lum[..., 0], stops[:, 0], stops[:, c + 1])
    graded = rgb * 0.30 + out * 0.70
    return Image.fromarray(np.dstack([np.clip(graded, 0, 255), a]).astype(np.uint8), "RGBA")


def crisp_small(full, name):
    """Small-size variant (96px): bolder outline, local contrast + unsharp mask so it survives 30-50px."""
    big = full.resize((SMALL * 4, SMALL * 4), Image.LANCZOS)
    a = big.getchannel("A")
    if name == "Wheat":
        # Thin stalks/awns vanish when minified: thicken the silhouette before the outline.
        a = a.filter(ImageFilter.MaxFilter(5))
    outline = a.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(1.2))
    canvas = Image.new("RGBA", big.size, (0, 0, 0, 0))
    ink = Image.new("RGBA", big.size, (46, 30, 16, 0))
    ink.putalpha(outline.point(lambda v: int(v * 0.95)))
    canvas = Image.alpha_composite(canvas, ink)
    body = big
    if name == "Wheat":
        # Fill the thickened silhouette with a blurred copy of the colour so gaps read as straw, not ink.
        fill = big.filter(ImageFilter.GaussianBlur(3)).copy()
        fill_rgb = np.asarray(fill, np.float32)[..., :3]
        fill_rgb = fill_rgb / np.maximum(np.asarray(fill, np.float32)[..., 3:] / 255.0, 0.05)
        fill_img = Image.fromarray(np.clip(fill_rgb * 0.9, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
        fill_img.putalpha(a)
        canvas = Image.alpha_composite(canvas, fill_img)
    canvas = Image.alpha_composite(canvas, body)
    small = canvas.resize((SMALL, SMALL), Image.LANCZOS)
    rgb = small.convert("RGB").filter(ImageFilter.UnsharpMask(radius=1.4, percent=90, threshold=1))
    from PIL import ImageEnhance
    rgb = ImageEnhance.Contrast(rgb).enhance(1.12)
    rgb = ImageEnhance.Color(rgb).enhance(1.18 if name in ("Wheat", "Stone") else 1.04)
    out = rgb.convert("RGBA")
    out.putalpha(small.getchannel("A"))
    return out


def main_v132():
    for src, name in NAMES.items():
        full = finish(cut(os.path.join(SRC, f"{src}.jpg")))
        if name == "Stone":
            full = tint_ore(full)
        full.save(os.path.join(DST, f"{name}.png"), optimize=True)
        crisp_small(full, name).save(os.path.join(DST, f"{name}Small.png"), optimize=True)
        print("wrote", name, "+ small")


if __name__ == "__main__" and os.environ.get("V132", "1") == "1":
    main_v132()
