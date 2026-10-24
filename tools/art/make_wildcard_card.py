"""v139: the trade wildcard ("any resource") card — the five painted resource icons fanned on one parchment card
with a thin ink outline. Output: Assets/_Project/Resources/Art/Icons/Painted/AnyResource.png (256x256 RGBA)."""
import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
ICONS = os.path.join(ROOT, "Assets/_Project/Resources/Art/Icons/Painted")
KIT = os.path.join(ROOT, "Assets/_Project/Resources/Art/UIKit/FrameThin.png")
OUT = os.path.join(ICONS, "AnyResource.png")

S = 512  # work at 2x, downsample at the end
CARD_W, CARD_H, RADIUS = 440, 480, 40
INK = (62, 38, 20, 255)


def parchment(w, h):
    kit = Image.open(KIT).convert("RGB")
    tile = kit.crop((40, 40, kit.width - 40, kit.height - 40))
    out = Image.new("RGB", (w, h))
    for y in range(0, h, tile.height):
        for x in range(0, w, tile.width):
            out.paste(tile, (x, y))
    return out


def main():
    canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    cx, cy = S // 2, S // 2 + 6
    box = (cx - CARD_W // 2, cy - CARD_H // 2, cx + CARD_W // 2, cy + CARD_H // 2)

    # Soft shadow.
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((box[0] + 8, box[1] + 12, box[2] + 8, box[3] + 12), RADIUS,
                                             fill=(30, 18, 8, 120))
    canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(9)))

    # Parchment card with a thin ink outline and an inner gold hairline.
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, RADIUS, fill=255)
    paper = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    paper.paste(parchment(CARD_W, CARD_H), (box[0], box[1]))
    canvas.paste(paper, (0, 0), mask)
    d = ImageDraw.Draw(canvas)
    d.rounded_rectangle(box, RADIUS, outline=INK, width=8)
    inset = 20
    d.rounded_rectangle((box[0] + inset, box[1] + inset, box[2] - inset, box[3] - inset), RADIUS - 14,
                        outline=(170, 126, 52, 255), width=3)

    # Five painted icons fanned over the card: an arc of three on top, two below, each tilted outward.
    names = ["Wood", "Brick", "Wheat", "Sheep", "Stone"]
    size = 176
    # (dx, dy, tilt degrees) per icon, relative to the card centre.
    spots = {"Wood": (-128, -70, 14), "Brick": (0, -96, 0), "Wheat": (128, -70, -14),
             "Sheep": (-70, 92, 7), "Stone": (70, 92, -7)}
    for name in ["Wood", "Wheat", "Brick", "Sheep", "Stone"]:
        dx, dy, tilt = spots[name]
        icon = Image.open(os.path.join(ICONS, name + ".png")).convert("RGBA").resize((size, size), Image.LANCZOS)
        rot = icon.rotate(tilt, resample=Image.BICUBIC, expand=True)
        # A faint dark rim so each icon separates from its neighbour.
        alpha = rot.split()[3]
        rim = Image.new("RGBA", rot.size, (40, 24, 10, 0))
        rim.putalpha(alpha.filter(ImageFilter.MaxFilter(7)).point(lambda v: int(v * 0.5)))
        x, y = int(cx + dx - rot.width / 2), int(cy + dy - rot.height / 2)
        canvas.alpha_composite(rim, (x, y))
        canvas.alpha_composite(rot, (x, y))

    canvas.resize((256, 256), Image.LANCZOS).save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
