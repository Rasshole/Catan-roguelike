# Board art pipeline (Colonist look)

## Runtime terrain (`Resources/Art/Board/`)

Kenney Hexagon Pack tiles baked to 256×256 PNGs (see `tools/import-kenney-board-art.py`):

| Slot | Kenney source |
|------|----------------|
| Wood | Grass `grass_11` |
| Sheep | Grass `grass_04` |
| Wheat | Grass `grass_02` |
| Brick | Dirt `dirt_04` |
| Ore | Stone `stone_05` |
| Desert | Sand `sand_09` |
| CoastSand | Sand `sand_03` |
| SeaLake | Grass `grass_06` (tinted ocean blue) |
| FishShoal | Project composite (fish on light water) |

`ColonistBoardTextureLoader` loads these at runtime; missing files fall back to `ColonistProceduralBoardTextures`.

## Icons (`Resources/Art/Icons/`)

Kenney Board Game Icons — see `ColonistKenneyResourceIcons` / `ColonistResourceIconTextures`.

## Boardgame UI (`Resources/Art/Boardgame/`)

Kenney Boardgame Pack dice faces and card back for colonist HUD (`ColonistKenneyDiceSprites`).

## 3D props (`Resources/Art/Models/Nature/`)

Quaternius FBX — `ColonistHexTileNatureDecor` on wood, stone, wheat hexes.

## Licenses

`Assets/_Project/Art/ART_LICENSES.md` and `Assets/_Project/Art/ThirdParty/`.
