# Board art licenses

## Kenney — Hexagon Pack (terrain albedos)

- **Files:** `Resources/Art/Board/Wood.png`, `Brick.png`, `Wheat.png`, `Sheep.png`, `Ore.png`, `Desert.png`, `SeaLake.png`, `CoastSand.png` (baked from Kenney terrain tiles)
- **License:** [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)
- **Source:** [Kenney Hexagon Pack](https://kenney.nl/assets/hexagon-pack) (OpenGameArt mirror used in `tools/import-kenney-board-art.py`)
- **Attribution:** Not required.

## Kenney — Board Game Icons (optional; board uses procedural icons)

- **Files:** `Resources/Art/Icons/*.png` (bundled for reference; **hex and port icons use procedural v87-style art** in `ColonistResourceIconTextures`)
- **License:** CC0 1.0
- **Source:** [Kenney Board Game Icons](https://kenney.nl/assets/board-game-icons)

## Kenney — Boardgame Pack (dice, card back)

- **Files:** `Resources/Art/Boardgame/Dice/DieWhite1–6.png`, `Resources/Art/Boardgame/Cards/CardBack_blue.png`
- **License:** CC0 1.0
- **Source:** [Kenney Boardgame Pack](https://kenney.nl/assets/boardgame-pack)
- **Note:** Number tokens on hexes still use **Roboto Slab** text (more readable than chip sprites at board scale).

## Quaternius — Ultimate Nature (3D props)

- **Files:** `Resources/Art/Models/Nature/NormalTree_2.fbx`, `BirchTree_*.fbx`, `Rock_3.fbx`, `Bush_*.fbx`
- **License:** CC0 1.0
- **Source:** [Quaternius Ultimate Nature](https://quaternius.com/packs/ultimatenature.html) (FBX via community Godot bundle mirror in `tools/import-kenney-board-art.py`)
- **Use:** Forest trees and sheep pasture bushes.

## Kenney — Medieval RTS (top-down terrain decor sprites)

- **Files:** `Resources/Art/Board/TopDownDecor/*.png` (wheat fields, windmill, rocks, mountain, brick/clay, fence, desert rock)
- **License:** CC0 1.0 — see `Art/ThirdParty/Kenney/medieval-rts-CC0.txt`
- **Source:** [Kenney Medieval RTS](https://kenney.nl/assets/medieval-rts) (OpenGameArt mirror in `tools/import-kenney-topdown-decor.py`)
- **Use:** Flat top-down props on wheat, stone, brick, desert, and sheep fence hexes (forest stays Quaternius 3D).

## Kenney — Hexagon Kit / Nature Kit (3D, legacy import)

- **Files:** `Resources/Art/Models/Kenney/*.fbx` (retained for tooling; board decor uses 2D sprites above)
- **License:** CC0 1.0 — `Art/ThirdParty/Kenney/hexagon-kit-CC0.txt`, `nature-kit-CC0.txt`
- **Import:** `tools/import-kenney-hex-decor.py`

## Procedural top-down desert cacti

- **Files:** `Resources/Art/Board/TopDownDecor/cactus_tall.png`, `cactus_short.png`
- **License:** Same project / CC0-style shapes generated in `tools/import-kenney-topdown-decor.py` (Medieval RTS pack has no cactus).

## Fishermen fishing-ground art

- **File:** `Resources/Art/Board/FishShoal.png` — light-blue water with simple fish motifs (project composite; chevron mesh tint in `ColonistCoastalFishShoalVisual`)

## Procedural fallback

- `tools/generate-board-art.py` remains for local regeneration if Kenney sources are absent; shipped builds use committed Kenney bakes above.

## Fonts

- **Roboto Slab Bold** — see `Assets/_Project/Art/Fonts/RobotoSlab-Apache-2.0.txt` (Apache 2.0).

## Reference (not redistributed)

Fishing-ground layout follows the *Fishermen of Catan* expansion (chevron frame, three border intersections). Catan™ trade dress is © Catan GmbH; this project uses original code and licensed CC0 art only.
