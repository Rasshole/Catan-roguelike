# Blender hex terrain tiles (v116)

Orthographic EEVEE renders at 1024×1024, inspired by physical Catan board photography.

## Regenerate

```bash
bash tools/blender/run-render.sh
```

Requires Blender 4.x and optionally `xvfb-run` on headless Linux.

## Outputs

| Path | Purpose |
|------|---------|
| `Assets/_Project/Resources/Art/Board/Blender/*.png` | Unity hex albedo (Git LFS) |
| `docs/art/blender-tiles/*_preview.png` | Per-terrain review stills |
| `docs/art/blender-tiles/contact_sheet.png` | 3×2 contact sheet |

Toggle `ColonistBlenderHexTerrainArt.Enabled` to restore v114 legacy textures and Kenney scatter decor.
