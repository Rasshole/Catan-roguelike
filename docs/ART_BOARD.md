# Board art pipeline (painted Catan look)

Place final painted terrain albedo textures under:

`Assets/_Project/Resources/Art/Board/`

Expected slot names (see `BoardTerrainSlot`):

- `Wood`, `Brick`, `Wheat`, `Sheep`, `Ore`, `Desert`, `SeaLake`, `CoastSand`

`ColonistBoardTextureLoader` loads these at runtime. Missing files fall back to procedural palette colors from `BoardResourceColors` / coast sand materials.

Reference target boards live alongside this doc when provided.
