# V1 Prototype — Implementation Status

Last updated: 2026-08-26 — P0 #7 AI risky shop penalty (efter P0 #4–#5 robber steal / AI shop afford).

## Done (playable prototype scope)

- [x] Unity 6.3 LTS (`6000.3.15f1`) project structure
- [x] **Built-in RP** (ikke URP) — se `docs/DESIGN_RENDERING.md`
- [x] **In-game map menu** (7 / 13 / 19 hex) at run start
- [x] **7 / 13 / 19-hex maps** (`MapSize`; 13 = classic shape minus 6 corners)
- [x] 3D placeholder board (table + hex cylinders)
- [x] Run start: leader select → draft 2 unique buildings
- [x] Setup: AI 2 settlements + roads, then player 2 + roads
- [x] Day/night turn loop
- [x] Yield rolls (15/55/25, max 1×0 and 1×2, 50/50 tie-break)
- [x] Rolls at night, production uses today's rolls
- [x] Multi-hex production per settlement/city
- [x] Catan placement rules (distance, roads, connectivity)
- [x] Catan costs + threshold pricing
- [x] Daily shop (3 deals) + **risky 3rd deal** (2:1, robber → best tile)
- [x] **Ports** — resource-specific 2:1 wired in shop
- [x] Cards: draw 1, play 1, max hand 5 — all 12 cards in `CardEngine`
- [x] Robber (tile block + steal on day move and Knight; seeded victim/resource pick)
- [x] Route sabotage (Bandit Raid card) + disabled road visuals
- [x] Longest route VP (≥5 roads)
- [x] 4 Leaders + level-ups every 5 days (max 3)
- [x] Draft 2 of 5 unique buildings
- [x] Random events (~22% per night)
- [x] AI heuristic + shop + limited card pool (shop gated on effective deal cost)
- [x] VP win at 10
- [x] Click-to-place on vertices and edges
- [x] Placeholder IMGUI
- [x] EditMode tests (rolls, placement, production, ports, **map sizes**, **bonus VP**, **VertexDistance**, **LongRoadBonus**, **longest-road blocking**, **robber steal**, **AI shop afford**, **risky shop penalty**)

## Explicitly out of scope

- [ ] Meta progression between runs
- [ ] Save/load
- [ ] Largest army VP
- [ ] Per-tile number tokens (classic Catan dice numbers)

## Known gaps (see `MISSING_AND_GAPS.md` for full list)

- [x] Bonus VP persists across refresh (Harbor Charter / FirstCityVp)
- [x] LongRoadBonus perk (+1 VP with longest route; drops when longest is lost)
- [ ] Generic 3:1 ports (API only)
- [ ] Bandit Raid road picker in UI
- [x] Longest road: opponent settlement blocking
- [ ] Real UI (uGUI) + art pass
- [x] Game.unity committed to repo
- [ ] Integration / full-run tests

## Map sizes

| `MapSize` | Hexes | Use case |
|-----------|-------|----------|
| Small | 7 | Fast / tutorial |
| Medium | 13 | Mid-size |
| Large | 19 | Classic Catan shape |

Set on **GameManager → Map Size** in the inspector (or re-run Setup Game Scene).

## How to test when back at PC

1. Open in **Unity 6.3 LTS (6000.3.15f1)**
2. **Catan Roguelike → Setup Game Scene** (if `Game.unity` missing)
3. On `GameManager`, set **Map Size** to Small / Medium / Large
4. Play — leader → draft → setup → day/night loop
5. Test Runner → EditMode → Run All
