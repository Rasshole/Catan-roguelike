# V1 Prototype — Implementation Status

Last updated: 2026-08-27 — Fase 2.6 meta progression on top of 2.5 hybrid tokens + 2.4 Act progression.

## Done (playable prototype scope)

- [x] Unity 6.3 LTS (`6000.3.15f1`) project structure
- [x] **Built-in RP** (ikke URP) — se `docs/DESIGN_RENDERING.md`
- [x] **In-game map menu** (7 / 13 / 19 hex) at run start
- [x] **7 / 13 / 19-hex maps** (`MapSize`; 13 = classic shape minus 6 corners)
- [x] 3D placeholder board (table + hex cylinders)
- [x] Run start: leader select → draft 2 unique buildings
- [x] Setup: AI 2 settlements + roads, then player 2 + roads
- [x] **Setup-bonus** — 2. settlement (spiller + AI) giver 1 af hver tilstødende ressource; desert springes over (`SetupBonusCalculator`)
- [x] Day/night turn loop
- [x] **Hybrid production (Fase 2.5)** — per-tile number tokens (2–12) gate which hexes fire; per-resource nightly rolls remain weather multipliers; 2d6 sums per yield pass (Act 2+ = 2 dice). See `docs/DESIGN_NUMBER_TOKENS.md`.
- [x] Yield rolls (15/55/25, max 1×0 and 1×2, 50/50 tie-break; Act 3 max 3)
- [x] Rolls + dice at night, production uses today's rolls + dice
- [x] Multi-hex production per settlement/city
- [x] Catan placement rules (distance, roads, connectivity)
- [x] Catan costs + threshold pricing
- [x] Daily shop (3 deals) + **risky 3rd deal** (2:1, robber → best tile)
- [x] **Ports** — resource-specific 2:1 + generic 3:1 wired in shop (`DiscoverPorts` sparse layout per map size)
- [x] Cards: draw 1, play 1, max hand 5 — all 12 cards in `CardEngine`
- [x] Robber (tile block + steal on day move and Knight; seeded victim/resource pick)
- [x] Route sabotage (Bandit Raid card) + disabled road visuals
- [x] Longest route VP (≥5 roads)
- [x] Largest army VP (≥3 played Knight cards; classic tie = incumbent keeps until surpassed)
- [x] 4 Leaders + level-ups every 5 days (max 3)
- [x] Draft 2 of 5 unique buildings
- [x] Random events (~22% per night in Act 1; scales by act)
- [x] **Act progression (Fase 2.4)** — `ActProgression`: days 1–5 Act 1, 6–10 Act 2, 11+ Act 3; double yield rolls Act 2+, max roll 3 Act 3, event chance/weights scale, AI extra night plays + Act 3 draw pool, Small→Medium→Large map growth
- [x] Event board overlays (storm marker, famine/gold rush/good harvest tints via `EventBoardVisual`)
- [x] AI heuristic + shop + limited card pool (Embargo in pool; skips embargoed shop Give; plays Embargo vs human inventory/shop)
- [x] VP win at 10
- [x] Click-to-place on vertices and edges
- [x] Placeholder IMGUI
- [x] EditMode tests (rolls, **number tokens (assignment, desert, hybrid production, expansion, save round-trip, AI/event robber targeting)**, placement, production, **setup-bonus (2nd settlement, desert skip)**, **ports (2:1 + 3:1 + priority)**, **map sizes**, **bonus VP**, **VertexDistance**, **LongRoadBonus**, **longest-road blocking**, **RouteCalculator disabled roads + loop/tie owner**, **Largest Army (grant/tie/overtake/breakdown/save)**, **robber steal**, **AI shop afford**, **AI Embargo pool + shop skip + play**, **AI Largest Army Knight priority**, **risky shop penalty**, **ShopGenerator embargo + MarketDay + deal generation**, **Monastery / RollInsurance night picks**, **Bandit Raid road target**, **pending status display**, **shop price reason**, **ShopDealDisplay risky shop button copy**, **VP breakdown**, **Architect threshold discount**, **level-up preview / RunProgression**, **RunProgression pre-game draft flow (map → leader → uniques → setup)**, **CardEngine all 12 cards**, **EventEngine all 6 events + timing**, **EventBoardVisual tile overlays**, **GameController integration (setup → day/night loop, win, level-up)**, **RunSummaryDisplay game-over summary**, **SaveGame round-trip JSON (format v1)**, **ActProgression thresholds + yield/events/AI/map expansion**, **MetaProgression (stars, unlock tree, meta.json isolation, award formula, purchase, start bonuses)**)
- [x] PlayMode smoke tests (`GameSceneSmokeTests` — `Game.unity` boot, required MonoBehaviours, `GameManager.Controller` after Start)

## Explicitly out of scope

- [x] **Meta progression between runs (Fase 2.6)** — stars earned on game over; unlock tree (maps, leaders, extra draft, start wheat/card); `meta.json` separate from `save.json`; IMGUI unlock panel on map select + game over
- [x] Save/load — **first slice:** JSON format v1, single slot, IMGUI Save/Load, round-trip test (autosave, slots, RNG roll counters deferred). Army fields are optional v1 properties with defaults. **Number tokens + dice rolls** are optional v1 tile/state fields.
- [x] Per-tile number tokens (classic Catan 2–12) — **hybrid model (b):** tokens + resource rolls + 2d6; see `docs/DESIGN_NUMBER_TOKENS.md`

## Known gaps (see `MISSING_AND_GAPS.md` for full list)

- [x] Bonus VP persists across refresh (Harbor Charter / FirstCityVp)
- [x] LongRoadBonus perk (+1 VP with longest route; drops when longest is lost)
- [x] Generic 3:1 ports (`DiscoverPorts` — sparse classic layout: specific 2:1 + generic 3:1 scaled to 7/13/19 hex)
- [x] Bandit Raid road picker in UI (IMGUI ◀/▶ when card selected)
- [x] Harbor Charter pending + Embargo status in IMGUI (`PendingStatusDisplay`)
- [x] Shop deal price reason in IMGUI (`ShopDealPricing`)
- [x] Risky shop deal consequence text in IMGUI (`ShopDealDisplay`)
- [x] VP breakdown in IMGUI (`VictoryBreakdown` — buildings / longest / long road / largest army / bonus)
- [x] Architect threshold discount — 10 % on threshold settlements only; Master Builder 0.65 vs 0.75 (no double discount)
- [x] Level-up preview on day before interval + full HUD during LevelUpChoice (`RunProgression.WillOfferLevelUpAfterThisDay`, `PendingStatusDisplay`)
- [x] AI Embargo — card in `AiPool`; human Embargo → `AiShopEmbargo`; AI Embargo → `PlayerShopEmbargo`; shop skip under embargo; Harbor Charter remains human-only (`aiCanUse: false`)
- [x] Longest road: opponent settlement blocking
- [x] Monastery + RollInsurance night-roll auto-picks match design text
- [ ] Real UI (uGUI) + art pass
- [x] Game.unity committed to repo
- [x] Integration / full-run tests (`GameControllerIntegrationTests` — day/night loop, 10 VP win, level-up on day 5)
- [x] Game-over run-summary in IMGUI (`RunSummaryDisplay` — winner, day, map, leader, seed, VP breakdown + Restart) + stars earned / unlock shop
- [x] P2 #23 — removed unused `GamePhase.DayEndCheck` and `DaySubPhase` enum

## Map sizes

| `MapSize` | Hexes | Use case |
|-----------|-------|----------|
| Small | 7 | Fast / tutorial — grows to 13 at Act 2, 19 at Act 3 |
| Medium | 13 | Mid-size — grows to 19 at Act 3 |
| Large | 19 | Classic Catan shape — no mid-run growth |

## Act progression (Fase 2.4)

| Act | Days | Yield | Events | AI | Map |
|-----|------|-------|--------|-----|-----|
| 1 | 1–5 | 1 resource roll pass + 1× 2d6, max mult 2 | 22% uniform | 1 night card play | start size |
| 2 | 6–10 | 2 resource passes summed + 2× 2d6, max mult 2 | 32%, hard events weighted | 2 night card plays, smarter pick | Small→Medium |
| 3 | 11+ | 2 resource passes summed + 2× 2d6, max mult 3 | 42%, harder weights | +1 AI draw, wider pool | →Large if not already |

Production: hex yields only when a **dice sum matches its number token** and the **resource multiplier > 0** (hybrid). IMGUI shows tokens on hexes + dice line.

Constants in `BalanceConfig`; logic in `ActProgression`. IMGUI shows current Act + unlock line.

Set on **GameManager → Map Size** in the inspector (or re-run Setup Game Scene). Medium/Large require meta unlocks unless purchased.

## Meta progression (Fase 2.6)

| Item | Detail |
|------|--------|
| File | `meta.json` in `persistentDataPath` (separate from `save.json`) |
| Currency | Stars = human VP + days÷2 + 2 on win (`MetaCatalog.WinBonusStars`) |
| Default free | Small map, Merchant + Pioneer, all 5 uniques in draft (pick 2) |
| Unlock tree | Medium/Large maps, Warlord/Architect leaders, +1 draft pick, +1 wheat at run start, Road Builder on first night |
| UI | `PlaceholderUI` — stars + Spend/Unlocks on map select and game over |
| Tests | `MetaProgressionTests` — award formula, persist/load, defaults, purchase, isolation from run save |

## How to test when back at PC

1. Open in **Unity 6.3 LTS (6000.3.15f1)**
2. **Catan Roguelike → Setup Game Scene** (if `Game.unity` missing)
3. On `GameManager`, set **Map Size** to Small / Medium / Large
4. Play — leader → draft → setup → day/night loop
5. Test Runner → EditMode → Run All
6. Test Runner → PlayMode → Run All (eller headless — se nedenfor)
