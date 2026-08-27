# V1 Prototype — Implementation Status

Last updated: 2026-08-27 — P2 game-over run-summary + event board overlays (`RunSummaryDisplay`, `EventBoardVisual`).

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
- [x] **Ports** — resource-specific 2:1 + generic 3:1 wired in shop (`DiscoverPorts` sparse layout per map size)
- [x] Cards: draw 1, play 1, max hand 5 — all 12 cards in `CardEngine`
- [x] Robber (tile block + steal on day move and Knight; seeded victim/resource pick)
- [x] Route sabotage (Bandit Raid card) + disabled road visuals
- [x] Longest route VP (≥5 roads)
- [x] 4 Leaders + level-ups every 5 days (max 3)
- [x] Draft 2 of 5 unique buildings
- [x] Random events (~22% per night)
- [x] Event board overlays (storm marker, famine/gold rush/good harvest tints via `EventBoardVisual`)
- [x] AI heuristic + shop + limited card pool (Embargo in pool; skips embargoed shop Give; plays Embargo vs human inventory/shop)
- [x] VP win at 10
- [x] Click-to-place on vertices and edges
- [x] Placeholder IMGUI
- [x] EditMode tests (rolls, placement, production, **ports (2:1 + 3:1 + priority)**, **map sizes**, **bonus VP**, **VertexDistance**, **LongRoadBonus**, **longest-road blocking**, **RouteCalculator disabled roads + loop/tie owner**, **robber steal**, **AI shop afford**, **AI Embargo pool + shop skip + play**, **risky shop penalty**, **ShopGenerator embargo + MarketDay + deal generation**, **Monastery / RollInsurance night picks**, **Bandit Raid road target**, **pending status display**, **shop price reason**, **ShopDealDisplay risky shop button copy**, **VP breakdown**, **Architect threshold discount**, **level-up preview / RunProgression**, **RunProgression pre-game draft flow (map → leader → uniques → setup)**, **CardEngine all 12 cards**, **EventEngine all 6 events + timing**, **EventBoardVisual tile overlays**, **GameController integration (setup → day/night loop, win, level-up)**, **RunSummaryDisplay game-over summary**)

## Explicitly out of scope

- [ ] Meta progression between runs
- [ ] Save/load
- [ ] Largest army VP
- [ ] Per-tile number tokens (classic Catan dice numbers)

## Known gaps (see `MISSING_AND_GAPS.md` for full list)

- [x] Bonus VP persists across refresh (Harbor Charter / FirstCityVp)
- [x] LongRoadBonus perk (+1 VP with longest route; drops when longest is lost)
- [x] Generic 3:1 ports (`DiscoverPorts` — sparse classic layout: specific 2:1 + generic 3:1 scaled to 7/13/19 hex)
- [x] Bandit Raid road picker in UI (IMGUI ◀/▶ when card selected)
- [x] Harbor Charter pending + Embargo status in IMGUI (`PendingStatusDisplay`)
- [x] Shop deal price reason in IMGUI (`ShopDealPricing`)
- [x] Risky shop deal consequence text in IMGUI (`ShopDealDisplay`)
- [x] VP breakdown in IMGUI (`VictoryBreakdown` — buildings / longest / long road / bonus)
- [x] Architect threshold discount — 10 % on threshold settlements only; Master Builder 0.65 vs 0.75 (no double discount)
- [x] Level-up preview on day before interval + full HUD during LevelUpChoice (`RunProgression.WillOfferLevelUpAfterThisDay`, `PendingStatusDisplay`)
- [x] AI Embargo — card in `AiPool`; human Embargo → `AiShopEmbargo`; AI Embargo → `PlayerShopEmbargo`; shop skip under embargo; Harbor Charter remains human-only (`aiCanUse: false`)
- [x] Longest road: opponent settlement blocking
- [x] Monastery + RollInsurance night-roll auto-picks match design text
- [ ] Real UI (uGUI) + art pass
- [x] Game.unity committed to repo
- [x] Integration / full-run tests (`GameControllerIntegrationTests` — day/night loop, 10 VP win, level-up on day 5)
- [x] Game-over run-summary in IMGUI (`RunSummaryDisplay` — winner, day, map, leader, seed, VP breakdown + Restart)
- [x] P2 #23 — removed unused `GamePhase.DayEndCheck` and `DaySubPhase` enum

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
