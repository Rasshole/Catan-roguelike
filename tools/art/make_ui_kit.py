#!/usr/bin/env python3
"""v132 painted UI kit: 9-slice parchment / wood / gold sprites cut from the v131 painted card frame
(Resources/Art/Cards/CardFrameWide.png) -> Resources/Art/UIKit/*.png. Every HUD panel, button and badge
uses these so the whole HUD shares the civ-pick / event-card look. Slice borders live in
ColonistUiKit.cs (keep in sync with BORDERS below)."""
import os
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RES = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art")
OUT = os.path.join(RES, "UIKit")
META_TEMPLATE = os.path.join(RES, "Cards", "CardFrameWide.png.meta")

SRC = Image.open(os.path.join(RES, "Cards", "CardFrameWide.png")).convert("RGB")
# Straight top edge: dark wood moulding, gold inlay, inner shadow onto parchment (rows 0..110).
PROFILE = SRC.crop((470, 0, 810, 110))
PROFILE_DEPTH = 96  # rows of PROFILE that are wood; the rest is the soft shadow onto the parchment
WOOD = SRC.crop((470, 28, 810, 58))  # plain plank grain, no inlay
PARCH = SRC.crop((420, 200, 860, 520)).filter(ImageFilter.GaussianBlur(1.2))

BORDERS = {}


def write_meta(png):
    meta = png + ".meta"
    if os.path.exists(meta):
        return
    lines = open(META_TEMPLATE).read().splitlines()
    lines[1] = "guid: " + uuid.uuid4().hex
    open(meta, "w").write("\n".join(lines) + "\n")


def parchment(w, h):
    p = PARCH.resize((max(w, 8), max(h, 8)), Image.LANCZOS)
    return np.asarray(p.crop((0, 0, w, h)), dtype=np.float32)


def wood_fill(w, h, tint=None):
    strip = WOOD.resize((max(int(WOOD.width * h / WOOD.height), w), h), Image.LANCZOS)
    a = np.asarray(strip.crop((0, 0, w, h)), dtype=np.float32)
    if tint is not None:
        lum = a.mean(axis=2, keepdims=True) / 70.0
        a = np.clip(lum * np.array(tint, dtype=np.float32), 0, 255)
    return a


def framed(w, h, depth, ornament=0, shadow=True, fill=None):
    """Mitered painted-wood frame `depth` px thick around parchment (or `fill`). ornament>0 pastes the
    painted gold corner pieces (scaled so the wood depth matches)."""
    total = int(round(depth * (110 if shadow else PROFILE_DEPTH) / PROFILE_DEPTH))
    prof_src = PROFILE if shadow else PROFILE.crop((0, 0, PROFILE.width, PROFILE_DEPTH))
    prof_w = max(int(prof_src.width * total / prof_src.height), 8)
    prof = np.asarray(prof_src.resize((prof_w, total), Image.LANCZOS), dtype=np.float32)
    img = fill.copy() if fill is not None else parchment(w, h)
    ys, xs = np.mgrid[0:h, 0:w]
    dt, db, dl, dr = ys, h - 1 - ys, xs, w - 1 - xs
    stack = np.stack([dt, db, dl, dr])
    side = stack.argmin(axis=0)
    d = stack.min(axis=0)
    along = np.where(side < 2, xs, ys) % prof_w
    mask = d < total
    sampled = prof[np.clip(d, 0, total - 1), along]
    if shadow:
        # shadow rows are multiplied onto whatever fill sits inside (keeps tinted fills tinted)
        wood_rows = d < depth
        base_parch = prof[total - 1, along]
        mul = sampled / np.maximum(base_parch, 1)
        shade = np.where(wood_rows[..., None], sampled, img * np.clip(mul, 0, 1.2))
        img = np.where(mask[..., None], shade, img)
    else:
        img = np.where(mask[..., None], sampled, img)
    out = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    if ornament:
        scale = depth / PROFILE_DEPTH
        size = int(round(175 * scale))
        corners = {
            (0, 0): SRC.crop((0, 0, 175, 175)),
            (1, 0): SRC.crop((SRC.width - 175, 0, SRC.width, 175)),
            (0, 1): SRC.crop((0, SRC.height - 175, 175, SRC.height)),
            (1, 1): SRC.crop((SRC.width - 175, SRC.height - 175, SRC.width, SRC.height)),
        }
        ramp = max(2, size // 8)
        wood_px = int(round(depth))
        for (cx, cy), crop in corners.items():
            patch = crop.resize((size, size), Image.LANCZOS)
            pa = np.asarray(patch, dtype=np.float32)
            yy, xx = np.mgrid[0:size, 0:size]
            ox = xx if cx == 0 else (size - 1 - xx)
            oy = yy if cy == 0 else (size - 1 - yy)
            in_wood = np.minimum(ox, oy) < wood_px
            # gold leaf: warm, saturated, mid-bright; parchment is paler and less saturated
            r, g, b = pa[..., 0], pa[..., 1], pa[..., 2]
            gold = ((r - b) > 62) & (r > 95) & (r < 225) & ((r - b) / np.maximum(r, 1) > 0.42)
            keep = in_wood.astype(np.float32)  # inner parchment ornament does not key cleanly at HUD size
            ix = (size - 1 - ox)
            iy = (size - 1 - oy)
            fade = np.clip(np.minimum(ix, iy) / ramp, 0, 1)
            a = Image.fromarray((keep * fade * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))
            out.paste(patch, (0 if cx == 0 else w - size, 0 if cy == 0 else h - size), a)
    return out


def soften(a, k=0.45):
    """Pull grain contrast toward the mean so buttons read as painted lacquer, not raw planks."""
    m = a.mean(axis=(0, 1), keepdims=True)
    return m + (a - m) * k


def rounded_mask(w, h, r, scale=4):
    m = Image.new("L", (w * scale, h * scale), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, w * scale - 1, h * scale - 1), r * scale, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def plaque(w, h, r, fill, rim=(196, 156, 74), rim_w=2, outline=(40, 24, 12)):
    """Rounded painted button / badge: fill array, dark outline, gold rim, soft top highlight."""
    base = Image.fromarray(np.clip(fill, 0, 255).astype(np.uint8)).convert("RGBA")
    ys = np.linspace(0, 1, h)[:, None, None]
    shade = 1.12 - 0.28 * ys  # light from the top
    arr = np.asarray(base, dtype=np.float32)
    arr[..., :3] = np.clip(arr[..., :3] * shade, 0, 255)
    base = Image.fromarray(arr.astype(np.uint8))
    s = 4
    layer = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle((0, 0, w * s - 1, h * s - 1), r * s, outline=outline + (255,), width=s * 1)
    d.rounded_rectangle((s, s, w * s - 1 - s, h * s - 1 - s), (r - 1) * s, outline=rim + (255,), width=s * rim_w)
    d.rounded_rectangle(
        ((1 + rim_w) * s, (1 + rim_w) * s, w * s - 1 - (1 + rim_w) * s, h * s - 1 - (1 + rim_w) * s),
        max(r - 1 - rim_w, 1) * s, outline=(30, 18, 8, 120), width=s)
    layer = layer.resize((w, h), Image.LANCZOS)
    out = Image.alpha_composite(base, layer)
    out.putalpha(rounded_mask(w, h, r))
    return out


def save(name, img, border):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    img.save(path, optimize=True)
    write_meta(path)
    BORDERS[name] = border
    print("wrote", os.path.relpath(path, ROOT), img.size, "border", border)


def main():
    # Large ornate frame (rail, dialogs, menus): 22 px painted wood + gold corner pieces.
    big = framed(200, 200, 22, ornament=1)
    save("FrameOrnate", big, 44)
    # Medium frame (bars, trade dialogs): 12 px wood, no ornaments, gold corner studs.
    med = framed(96, 96, 12)
    dm = ImageDraw.Draw(med)
    for (x, y) in ((5, 5), (90, 5), (5, 90), (90, 90)):
        dm.ellipse((x - 2.5, y - 2.5, x + 2.5, y + 2.5), fill=(214, 172, 86), outline=(70, 44, 18))
    save("FrameMedium", med, 20)
    # Inset card (player panels, log, chat, resource cards): thin 5 px wood rim + gold line.
    thin = framed(48, 48, 5)
    save("FrameThin", thin, 10)
    # Parchment chip (cost chips, stat pills): parchment, 1.5 px brown rim, rounded.
    chip = plaque(48, 32, 7, parchment(48, 32) * 1.02, rim=(150, 112, 62), rim_w=1, outline=(78, 52, 28))
    save("Chip", chip, 10)
    # Buttons (cream text): painted green lacquer and dark wood, gold rim.
    green = plaque(96, 48, 10, soften(wood_fill(96, 48, tint=(64, 112, 52))))
    save("ButtonGreen", green, 14)
    wood = plaque(96, 48, 10, wood_fill(96, 48) * 1.15)
    save("ButtonWood", wood, 14)
    red = plaque(96, 48, 10, soften(wood_fill(96, 48, tint=(146, 58, 42))))
    save("ButtonRed", red, 14)
    parch_btn = plaque(96, 48, 10, parchment(96, 48), rim=(168, 128, 66), rim_w=2, outline=(70, 44, 20))
    save("ButtonParchment", parch_btn, 14)
    # Round count badge: dark wood, gold rim.
    badge = plaque(32, 32, 16, wood_fill(32, 32) * 0.95, rim_w=2)
    save("Badge", badge, 15)
    # Turn banner plaque: dark wood with gold inlay frame (cream text).
    banner_fill = np.asarray(Image.fromarray(wood_fill(160, 56).astype(np.uint8)), dtype=np.float32) * 1.1
    banner = framed(160, 56, 7, shadow=False, fill=banner_fill)
    save("Banner", banner.convert("RGBA"), 16)
    print(BORDERS)


if __name__ == "__main__" and os.environ.get("V133", "1") != "1":
    main()


# ---------------------------------------------------------------------------------------------- v133
# "Parchment everywhere": every light piece (FrameOrnate/Medium/Thin, Chip, ButtonParchment) now carries the
# leader card's old-scroll parchment (Resources/Art/Cards/CardFramePortrait.png) at native detail instead of a
# blurred, stretched crop. Layout per piece: [B | L | B] where the L x L centre is a SEAMLESS parchment tile
# and the B-wide border holds the wood frame plus the card's darker mottled aged edge. ColonistUiKit draws
# these with Image.Type.Tiled, so the centre repeats at native scale (never stretches) and the edge strips
# repeat with the same period L (the wood grain and edge mottle are built periodic in L, so they tile too).
CARD = Image.open(os.path.join(RES, "Cards", "CardFramePortrait.png")).convert("RGB")
CARD_ARR = np.asarray(CARD, dtype=np.float32)
PARCH_MEAN = CARD_ARR[400:800, 250:600].mean(axis=(0, 1))


def _crossfade_periodic(a, axis):
    """Make an array seamless along `axis`: blend with itself rolled by half, weighted to hide both seams."""
    n = a.shape[axis]
    t = np.abs(np.linspace(-1, 1, n, dtype=np.float32))  # 1 at the ends, 0 in the middle
    w = np.clip((t - 0.15) / 0.7, 0, 1)
    w = w * w * (3 - 2 * w)
    shape = [1] * a.ndim
    shape[axis] = n
    w = w.reshape(shape)
    rolled = np.roll(a, n // 2, axis=axis)
    return a * (1 - w) + rolled * w


def parchment_tile(size):
    """Seamless size x size tile of the card parchment: 576 px crop, flattened (no vignette), wrapped."""
    crop = CARD_ARR[260:900, 112:752]
    low = np.asarray(Image.fromarray(crop.astype(np.uint8)).filter(ImageFilter.GaussianBlur(110)), np.float32)
    flat = crop / np.maximum(low, 1) * PARCH_MEAN  # remove the card's own large-scale shading
    t = _crossfade_periodic(_crossfade_periodic(flat, 0), 1)
    img = Image.fromarray(np.clip(t, 0, 255).astype(np.uint8)).resize((size, size), Image.LANCZOS)
    a = np.asarray(img, dtype=np.float32)
    # the 3x minification (and the crossfade) halves the mottle; restore the card's grain contrast
    m = a.mean(axis=(0, 1), keepdims=True)
    return m + (a - m) * 1.8


def edge_band(length, depth):
    """Ratio map (depth x length, periodic along length) of the card's aged inner edge: the soft frame shadow
    and the darker mottled, torn-looking parchment rim, relative to the clean centre parchment."""
    # left inner edge (cols 62..202) of the portrait card, turned to run along the strip
    strip = CARD_ARR[200:952, 62:202].transpose(1, 0, 2)
    ratio = (strip / PARCH_MEAN) ** 1.6  # deepen the aged rim a little for small HUD panels
    ratio = _crossfade_periodic(ratio, 1)
    img = Image.fromarray(np.clip(ratio * 160, 0, 255).astype(np.uint8)).resize((length, depth), Image.LANCZOS)
    r = np.asarray(img, dtype=np.float32) / 160.0
    fade = np.clip(np.linspace(1, 0, depth, dtype=np.float32) / 0.35, 0, 1)  # exactly 1.0 at the tile edge
    return 1 + (np.minimum(r, 1.03) - 1) * fade[:, None, None] * 1.15


def periodic_wood_profile(length, depth):
    prof = PROFILE.crop((0, 0, PROFILE.width, PROFILE_DEPTH)).resize((length, depth), Image.LANCZOS)
    return _crossfade_periodic(np.asarray(prof, dtype=np.float32), 1)


def tiled_canvas(B, L):
    tile = parchment_tile(L)
    n = 2 * B + L
    ys, xs = np.mgrid[0:n, 0:n]
    return tile[(ys - B) % L, (xs - B) % L], ys, xs


def apply_band(img, ys, xs, B, L, start, band):
    n = img.shape[0]
    r = edge_band(L, band)
    for d, along in ((ys, xs), (n - 1 - ys, xs), (xs, ys), (n - 1 - xs, ys)):
        k = d - start
        m = (k >= 0) & (k < band)
        idx = np.clip(k, 0, band - 1)
        img = np.where(m[..., None], img * r[idx, (along - B) % L], img)
    return img


def framed_tiled(B, L, depth, band, ornament=False):
    img, ys, xs = tiled_canvas(B, L)
    n = img.shape[0]
    img = apply_band(img, ys, xs, B, L, depth, band)
    prof = periodic_wood_profile(L, depth)
    stack = np.stack([ys, n - 1 - ys, xs, n - 1 - xs])
    side = stack.argmin(axis=0)
    d = stack.min(axis=0)
    along = (np.where(side < 2, xs, ys) - B) % L
    mask = d < depth
    img = np.where(mask[..., None], prof[np.clip(d, 0, depth - 1), along], img)
    out = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    if ornament:
        scale = depth / PROFILE_DEPTH
        size = int(round(175 * scale))
        corners = {
            (0, 0): SRC.crop((0, 0, 175, 175)),
            (1, 0): SRC.crop((SRC.width - 175, 0, SRC.width, 175)),
            (0, 1): SRC.crop((0, SRC.height - 175, 175, SRC.height)),
            (1, 1): SRC.crop((SRC.width - 175, SRC.height - 175, SRC.width, SRC.height)),
        }
        ramp = max(2, size // 8)
        for (cx, cy), crop in corners.items():
            patch = crop.resize((size, size), Image.LANCZOS)
            yy, xx = np.mgrid[0:size, 0:size]
            ox = xx if cx == 0 else (size - 1 - xx)
            oy = yy if cy == 0 else (size - 1 - yy)
            keep = (np.minimum(ox, oy) < depth).astype(np.float32)
            fade = np.clip(np.minimum(size - 1 - ox, size - 1 - oy) / ramp, 0, 1)
            a = Image.fromarray((keep * fade * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))
            out.paste(patch, (0 if cx == 0 else n - size, 0 if cy == 0 else n - size), a)
    return out


def parchment_plaque(B, L, r, band, rim=(168, 128, 66), rim_w=2, outline=(70, 44, 20)):
    """Rounded parchment button / chip with an aged edge; light bevel only inside the border."""
    img, ys, xs = tiled_canvas(B, L)
    n = img.shape[0]
    img = apply_band(img, ys, xs, B, L, rim_w + 1, band)
    # bevel: top border a touch lighter, bottom border a touch darker (centre untouched so it tiles)
    top = np.clip((B - ys) / B, 0, 1)[..., None]
    bot = np.clip((ys - (n - 1 - B)) / B, 0, 1)[..., None]
    img = img * (1 + 0.06 * top - 0.14 * bot)
    base = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).convert("RGBA")
    s = 4
    layer = Image.new("RGBA", (n * s, n * s), (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    dr.rounded_rectangle((0, 0, n * s - 1, n * s - 1), r * s, outline=outline + (255,), width=s)
    dr.rounded_rectangle((s, s, n * s - 1 - s, n * s - 1 - s), (r - 1) * s, outline=rim + (255,), width=s * rim_w)
    layer = layer.resize((n, n), Image.LANCZOS)
    out = Image.alpha_composite(base, layer)
    out.putalpha(rounded_mask(n, n, r))
    return out


def main_v133():
    # (border B, tile L): keep in sync with ColonistUiKit.Borders (border = B).
    big = framed_tiled(74, 256, 22, 52, ornament=True)
    save("FrameOrnate", big, 74)
    med = framed_tiled(42, 256, 12, 30)
    dm = ImageDraw.Draw(med)
    n = med.width
    for (x, y) in ((5, 5), (n - 6, 5), (5, n - 6), (n - 6, n - 6)):
        dm.ellipse((x - 2.5, y - 2.5, x + 2.5, y + 2.5), fill=(214, 172, 86), outline=(70, 44, 18))
    save("FrameMedium", med, 42)
    save("FrameThin", framed_tiled(22, 192, 5, 17), 22)
    save("Chip", parchment_plaque(12, 96, 7, 8, rim=(150, 112, 62), rim_w=1, outline=(78, 52, 28)), 12)
    save("ButtonParchment", parchment_plaque(16, 128, 10, 10), 16)
    print(BORDERS)


if __name__ == "__main__" and os.environ.get("V133", "1") == "1":
    main_v133()
