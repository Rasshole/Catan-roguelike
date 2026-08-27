# Agent log

## 2026-08-26 (PT) — Fase 0 uden Unity-licens

Landet på `main` (ingen Unity `.ulf`, så 0.1/0.2/0.7 ikke rørt; ingen fake `.meta` / `Game.unity`):

- **0.4** `.gitattributes` (line endings, Unity YAML merge=binary, Git LFS for binære assets)
- **0.5** Built-in RP — URP-shader-kald erstattet med `Standard` via `BuiltInMaterials`; `docs/DESIGN_RENDERING.md`
- **0.6** `tools/verify-fresh-clone.sh` + `docs/TOOLING.md` (licens → exit 2, hænger ikke; `Game.unity` mangler = WARN)
- **Blockers** `docs/BLOCKED.md` — Unity-licens + Mac standalone-modul 404 fra Linux
- **Core** kompilerer under `dotnet` net8.0 (manglende sibling-usings + `isCoastal:`). Ingen sim-runner i dette pass.

**Næste:** Unity `.ulf` → 0.1 (`.meta`) / 0.2 (`ProjectSettings`) / 0.7 (`Game.unity`), derefter sim-runner.

## 2026-08-26 (CEST) — P0 #1 + sim-runner + editor debug

- **P0 #1 bonus-VP:** `AddVictoryPoints` skriver nu `PlayerBonusVictoryPoints` / `AiBonusVictoryPoints`. `RefreshVictoryPoints` = board (bygninger + longest road) + bonus. Harbor Charter og FirstCityVp overlever refresh. Tests: `HarborCharter_BonusVp_SurvivesRefreshVictoryPoints`, `FirstCityVp_BonusVp_SurvivesRefreshVictoryPoints`, `RefreshVictoryPoints_WithoutBonus_CountsOnlyBoardVp` (kørt via `dotnet test tools/core-tests`). LongRoadBonus **ikke** rørt.
- **Sim-runner:** `tools/sim-runner` net8.0, Core via glob. Per-run timeout 5s + max-steps/max-days. Fuld placement **staller** i `VertexGraph.VertexDistance` efter første building (verificeret: `SetupPlayerSettlement1`). Driver = `narrow-core` (setup-inject + nat/dag uden AI-placement). `--runs 5` ~1.3s, 5× `max_days`, 0 timeout/crash.
- **Editor debug:** `Catan Roguelike/Debug` i `DebugHooks.cs` (`#if UNITY_EDITOR` + Editor asmdef). Fast-forward, resources/VP, force roll/card/event, auto-play, seed replay, panel + genveje. `GameManager.DebugRestart` kun under `UNITY_EDITOR`. **Ikke Unity-verificeret** (ingen `.ulf`).
- Ingen fake `.meta` / `Game.unity`. Ingen v0.1-tag.

**Næste:** Unity `.ulf` → 0.1/0.2/0.7. P0 #2 LongRoadBonus. `VertexDistance`-stall blokerer fuld AI-vs-AI sim.

## 2026-08-26 (CEST) — VertexDistance hang

- **Root cause:** `Canonicalize` was not idempotent — the (c+2)/(c+3) neighbor remap walked toward −Q, so BFS `visited` grew without bound on the infinite hex grid.
- **Fix (Core):** corners CW from north vs directions CCW from east → other hexes are neighbors (c+4) and (c+5). `Canonicalize` / `GetHexesForVertex` / `GetAdjacentVertices` agree; `Canonicalize` is idempotent.
- **Tests** (`tools/core-tests`): `Canonicalize_IsIdempotent_ForEveryBoardCorner`, `Canonicalize_EquivalentRepresentations_ShareTheSameCanonicalVertex`, `VertexDistance_TerminatesOnBoardWithTwoSettlementsAndRoads`, `VertexDistance_SameVertexDifferentRepresentations_IsZero`, `GetValidSettlementSpots_ReturnsInBoundedTime_WithBuildingsPresent`, `CanPlaceSettlement_ReturnsInBoundedTime_WithBuildingsPresent`.
- **Sim-runner:** default driver `full` (`PlaceSettlement` / `EndPlayerDay`). `--runs 3` @ 5s: 2× `ok` (human), 1× `max_days`, 0 timeout/crash. Timeout-guards kept. `TodayRolls` still copied before first night so AI night-plan does not crash.

**Næste:** P0 #2 LongRoadBonus. Unity `.ulf` → 0.1/0.2/0.7. Editor debug-hooks still skip `EndPlayerDay`.

## 2026-08-26 (CEST) — P0 #2 LongRoadBonus

- Wired in `VictoryCalculator.RefreshVictoryPoints`: human with perk + longest route gets +1 VP on top of the regular 2. Not stored in `PlayerBonusVictoryPoints` (would stick after losing longest). Second refresh neither drops nor doubles it.
- Tests: `LongRoadBonus_HasPerkAndLongest_AddsOneVp`, `LongRoadBonus_LosesLongest_BonusGone`, `LongRoadBonus_DoesNotDoubleCountRegularLongestRouteBonus`.
- Docs: `MISSING_AND_GAPS`, `IMPLEMENTATION_STATUS`. P0 #3 opponent blocking **not** done.

## 2026-08-26 (CEST) — P0 #3 longest-road opponent blocking

- Classic Catan: DFS in `RouteCalculator` no longer paths through an opponent settlement/city. Own buildings do not split the chain. Empty board = 0; disjoint chains report the longer one.
- `VertexGraph.Canonicalize` unchanged (9fb1f98).
- Tests: `ContinuousRoadOfLengthN_WithoutEnemyBuildings_EqualsN`, `EnemySettlementInMiddle_SplitsChain_ReportsLongerPieceNotSum`, `OwnSettlement_DoesNotSplitOwnRoads`, `NoRoads_ReturnsZero`, `TwoDisjointChains_ReportsTheLongerOne`, `TwoDisjointChainsOfEqualLength_ReportsThatLength`, `VertexDistance_TerminatesWithBuildingsPresentOnRoadChain`.
- Not in this pass: robber (P0 #4), loop/branch trail rewrite.

## 2026-08-26 (CEST) — AI setup second settlement

- `AdvanceSetupAfterRoad` after `SetupAiRoad1` jumped to `SetupPlayerSettlement1`, so `SetupAiSettlement2` never ran (AI placed 1 settlement). Now advances to settlement 2 and continues the AI setup chain; after road 2 the player places settlement 1.
- Test: `ConfirmRunSetup_AiPlacesTwoSettlementsAndTwoRoads`.

## 2026-08-27 (UTC) — ROADMAP_V3 (docs only)

- **`docs/ROADMAP_V3.md`:** post-Fase 2 founder backlog (12 kandidater, impact×effort, Linux-VM feasibility). **PICK:** sim-driven day-ceiling / win-rate balance pass (pre-2.4 sim ~790/1000 `max_days` — kendt risiko, ikke genmålt). Acceptance: sim metrics, win-rate/dage targets, 297 EditMode grøn.
- Pointers i `IMPLEMENTATION_STATUS.md` + `MISSING_AND_GAPS.md` (erstatter stale longest-road-algoritme-rækkefølge). Ingen C# / gameplay-ændringer.

**Næste:** implementér balance-pass per `ROADMAP_V3.md` acceptance.

## 2026-08-27 (UTC) — sim-runner unblock + metrics

- `tools/sim-runner`: Newtonsoft.Json 13.0.3 (Fase 2.6 compile fix). `endAct` + summary `max_days` / `medianMaxDaysPlayerVp`. Balance knobs unchanged.

## 2026-08-27 (UTC) — sim-driven day-ceiling / win-rate balance pass

**Before** (main @ 33b9154, `--runs 200 --max-days 20 --map small`): ok 12.5%, max_days 87.5%, median win day 20, median human VP at max_days 4, human VP=2 stall 70/175.

**After** (same flags): ok 89.5%, max_days 10.5%, median win day 13, median human VP at max_days 7, human VP=2 at max_days 1/21, 0 crash/timeout. VictoryPointGoal stays 10.

**Knobs:** `BalanceConfig` (roll weights 5/45/50, act days 1–4/5–8/9+, 2/3/3 dice passes, max mult 3 Act 2+, city 1W+2S, settlement threshold 7, longest route min 3 / 3 VP, largest army threshold 2 / 3 VP, lower event %), hybrid production floor (dice match → min 1 yield), setup bonus ×2, Small-map robber on `(1,-1)`, `LevelUpIntervalDays` 3. **Sim-driver:** road→settlement→city build order, scored settlements, VP-aware level-up picks. **Tests:** 299/299 `dotnet test tools/core-tests`.

## 2026-08-27 (UTC) — meta pool locks (unique + card draft/draw gating)

**Fresh meta:** draft pool = Sawmill + Guild Hall (pick 2); night draw = 7 starter cards (Knight, Road Builder, Year of Plenty, Monopoly, Drought, Master Builder, Fertile Season). **Unlocks:** Monastery (3★), Caravan Post (4★), Fortress Outpost (5★); Sabotage pack — Bandit Raid + Embargo (3★); Market pack — Harbor Charter + Merchant's Ledger + Forecast (4★). Extra draft pick capped at available uniques. **AI:** full `AiPool` unchanged. **Tests:** 307/307 `dotnet test tools/core-tests`.

## 2026-08-27 (UTC) — autosave + multi-slot save/load (Fase 2.1 rest)

- **Autosave:** `GameController.OnAutosavePoint` fires when night resolves to `DayPlayerActions` (after production + shop). `GameManager.AutosaveRun()` writes slot 0 with `isAutosave` + UTC timestamp.
- **Slots:** `SaveGameSlotStore` — slot 0 = `save.json` (legacy), slot 1 = `save_1.json`. `SaveGameFile` / IMGUI Save 1/2 + Load 1/2. Load re-binds `MetaProgression`; `meta.json` untouched.
- **Format v1 extras:** optional `savedAtUtc`, `isAutosave`, `metaStartCardGranted` (inferred when absent).
- **RNG roll counters:** not added — existing roll lists sufficient for resume; documented in `SaveGame` remarks.
- **Tests:** `SaveGameSlotsTests` (autosave hook, slot isolation, legacy load, metadata). **312/312** `dotnet test tools/core-tests`.

## 2026-08-27 (UTC) — Act 3 content variation (events)

- **`PortBlockade`** — Act 3-only (weights 0/0/2): picks a random port vertex; that harbor gives no 2:1/3:1 discount today (`PortAccess` + `ShopGenerator`); coastal hex overlay. Save v1 optional `EventBlockedPortVertex`.
- **`ResourceLevy`** — Act 3-only (weights 0/0/2): human loses 1 of their most abundant resource when the event fires (text-only status via `EventMessage`).
- Existing 6 events unchanged in Act 1–3 pools. No new card (save/UI cost skipped). **`HybridProductionTests` desert case** — clear neighbor tokens so RNG token shuffle cannot flake.
- **Tests:** `Act3EventTests` + overlay/save updates. **333/333** `dotnet test tools/core-tests`.

## 2026-08-27 — First-run onboarding beats (IMGUI)

- **`OnboardingCopy`** — pure Core helper: phase banners (map, leader, draft, setup, day-1 night/day, game over), hybrid-yield hint, stars persist line. `OnboardingTipsStore` uses `PlayerPrefs` (default on).
- **`PlaceholderUI`** — Tips toggle + cyan banners; hybrid hint near dice on first night; game-over meta line after star award.
- **Copy fixes:** Forecast card text mentions 2d6 reroll; Famine + Good Harvest event descriptions say "tomorrow" (matches `TomorrowRolls`).
- **Tests:** `OnboardingCopyTests`, Famine message assertion. **323/323** `dotnet test tools/core-tests`.

## 2026-08-27 — PlayMode harness beyond smoke

- **`GameScenePlayTests`** — 2 UnityTests in loaded `Game.unity`: pre-game → setup phase; full setup + skip night + `EndPlayerDay` → `DayNumber >= 1`, no winner. Uses `GameController` public APIs (`SelectMap`, draft, `PlaceSettlement`/`PlaceRoad`, `SkipNightCard`, `EndPlayerDay`). No `DebugHooks` (editor-only).
- **`GameSceneSmokeTests`** unchanged (2 boot tests).
- **Docs:** `BLOCKED.md` #3 Windows-Mono tarball 404 from Linux-VM; `ROADMAP_V3` PlayMode + Windows build rows updated.
- **Core:** `dotnet test tools/core-tests` unchanged.

## 2026-08-27 (UTC) — Robber/storm markers above hex cylinders

- **`HexTileView`:** robber sphere and storm cube raised to parent-local y=1.28 (above chip/label); XZ offsets so robber sits toward a hex corner and storm the opposite side. `localScale` divided by hex `lossyScale` for ~0.30 world robber diameter and ~0.22 storm slab. Token chip constants unchanged.

## 2026-08-27 (UTC) — Number-token chip visibility above hex cylinders

- **`HexTileView`:** chip/label elevation raised to parent-local y≈1.08/1.18 (cylinder top = 1.0); disc `localScale` compensated for hex `lossyScale` so world rim ~0.62, face ~0.52, thickness ~0.04. Robber/storm markers unchanged. **`NumberTokenVisualStyle`** colors untouched.


- **`NumberTokenVisualStyle`:** darker standard rim + slightly darker cream face; stronger `ChipRimRedAccent` so 6/8 rims read on wheat/sheep hexes. **`HexTileView`:** chip rim/face diameters 0.44 / 0.36 (was 0.42 / 0.34).
- **Tests:** `PaletteConstants_MatchExpectedRgbValues` in `NumberTokenVisualStyleTests`. **339/339** `dotnet test tools/core-tests`.

## 2026-08-27 (UTC) — Token/hex visual pass (placeholder chips)

- Rebased onto `main` @ bb499db+; merged with Act 3 `PortBlockade` overlay/tint from #61.
- **`NumberTokenVisualStyle` (Core):** cream chip face + rim colors; red accent for 6/8; label sizing for two-digit tokens.
- **`HexTileView`:** flat disc chip (rim + face) slightly above hex top; `TextMesh` centered on chip; idempotent `EnsureTokenChip`; desert hides chip + label. Built-in `Standard` via `BuiltInMaterials`. Robber / storm / port-blockade tints unchanged.
- **Tests:** `NumberTokenVisualStyleTests` (5 cases). **338/338** `dotnet test tools/core-tests` (expected after merge).
- **Docs:** `ROADMAP_V3` — token visual pass + PortDiscount marked done on top of existing Done rows (Act 3, PlayMode, Windows blocked).

## 2026-08-27 (UTC) — Hex prism tiles

- **`HexPrismMesh`:** pointy-top 6-sided prism (y=±1, radius 0.5) aligned with `HexMath` corner 0 at +Z; `BoardView` uses `hexScale * 2` XZ so circumradius matches settlement vertices.
- **`BoardView.CreateHexTile`:** custom mesh + `MeshCollider` instead of `PrimitiveType.Cylinder`; chip/robber/storm elevations unchanged.
- **Tests:** `HexPrismMeshTests` (EditMode). Core `dotnet test` unchanged.

## 2026-08-27 (UTC) — IMGUI HUD layout + table camera framing

- **`PlaceholderHudLayout`:** in-run/setup/day/night panel 260×580 max (was 400×full height); pre-game map/leader/draft stays 400px wide. Exposes board screen-offset helper for camera framing.
- **`PlaceholderUI`:** uses phase-aware panel rect; reports active width for camera.
- **`TableCamera`:** after orbit, shifts look-at so board center sits in the unobstructed region right of the HUD (BoardInputController still uses `Camera.main`).
- **Tests:** `PlaceholderHudLayoutTests` (EditMode). No uGUI rewrite; no v0.1 tag.

## 2026-08-27 (UTC) — TableCamera framing fix (PR #69)

- **`ApplyBoardFramingOffset`:** LookAt(+worldOffsetX) pushed the board left on screen (inverted). Now translates camera by `-(worldShifted - worldAtBoard)` then re-LookAt board center — board slides right into the HUD-free region.
- **`Start()`:** calls the same orbit + framing path so frame 0 matches Update (no default-transform flash).

## 2026-08-27 (UTC) — TableCamera board-fit zoom (post #69)

- **`TableCameraFraming`:** orbit distance/height derived from hex-cluster bounding radius (`HexMath` centers + outer radius × `hexScale`), not the decorative table surface. Small (~3.13 @ 1.2 scale) ≈ 2.6 orbit distance vs old fixed 8.
- **`TableCamera`:** reads `BoardView.GetBoardBoundingRadius()` each frame; HUD horizontal framing unchanged.
- **Tests:** `TableCameraFramingTests` (Small closer than Large/Medium). No uGUI; no v0.1 tag.

## 2026-08-27 (UTC) — BoardView bounding-radius fallback fix

- **`GetBoardBoundingRadius`:** use built `_tiles.Keys`, else live `BoardState` keys; return `0` before init so `MinOrbitDistance` applies. Removed per-frame `MapPresets.CreateBoard` fallback (CS0103 + allocation).

## 2026-08-27 (UTC) — TableCamera distance ratio retune

- **`DistanceToRadiusRatio`:** 0.78 → **2.0** (~2.16× radius with margin). Small orbit ~6.8 (> cluster radius ~3.1) so the 7-hex flower is not clipped; still closer than old fixed 8 for modest fill in the HUD-free region.
- **`TableCamera`:** enforce `nearClipPlane` ≥ 0.3 to reduce hex-top z-fighting at orbit pitch.
- **Tests:** orbit distance > board radius for Small/Large; ordering only, no magic radius floats.

## 2026-08-27 (UTC) — AiController.PlaceSetupSettlement EditMode tests

- **`AiControllerSetupSettlementTests`:** valid setup spot + Catan distance; same seed → same vertex after seeding tokens (scoring is pure; `CreateBoard` tokens are otherwise unseeded); no-op when no valid spots; highest-score pick. Isolated count==1 uses a non-chaining setup phase because `PlaceSettlement` auto-runs AI road/settlement 2 from `SetupAiSettlement1`. No production bug found.

## 2026-08-27 (UTC) — AiController.PlaceSetupRoad EditMode tests

- **`AiControllerSetupRoadTests`:** valid setup road touches AI settlement, same seed → same edge, no-op when no settlement or all adjacent edges blocked. No production bug found.

## 2026-08-27 (UTC) — Placement hover ghost silhouettes

- **`PlacementMarkerVisuals`:** settlement/city/road hover markers reuse `PlayerPieceVisuals` geometry with translucent `BuiltInMaterials.CreateGhost` tints (no sphere/cube debug primitives).
- **`BoardInputController`:** spawns ghosts at `BoardView.TileHeight` via the helper; pick/hit logic unchanged.
- **Tests:** `PlacementMarkerVisualsTests` (EditMode). No v0.1 tag.

## 2026-08-27 (UTC) — Sea ring around wood board disk

- **`TableCameraFraming`:** `WaterSurfacePadFactor` (1.55× wood radius, clamped ≤1.75×) + `ComputeWaterDiskScale` / world-radius helpers; camera orbit still uses hex bounding radius only.
- **`BoardView`:** runtime `WaterSurface` cylinder behind `BoardSurface` (`BoardWaterMaterial`, dark sea albedo).
- **Tests:** `BoardSurfaceSizingTests` water sizing/Y; `BoardWaterMaterialTests`. No v0.1 tag.

## 2026-08-27 (UTC) — Game-view screenshot tool (`GameViewCapture`)

- **`GameViewCapture`:** MenuItem + `CaptureAndQuit` (`-executeMethod`); Play Mode scripted setup via shared `GameScenePlayHarness`, `Camera.main` → 1920×1080 PNG (`/workspace/game-view.png` or `GAME_VIEW_SHOT`). Exits Play Mode without saving `Game.unity`.
- **Tests:** `GameScenePlayHarnessTests` (EditMode). Documented in `docs/TOOLING.md`.

## 2026-08-27 (UTC) — Felt tablecloth under sea ring

- **`TableCameraFraming`:** `FeltSurfacePadFactor` (1.45× water radius, clamped ≤1.6×) + `ComputeFeltDiskScale` / world-radius helpers; camera orbit still hex-only.
- **`BoardView`:** runtime `FeltSurface` cylinder behind `WaterSurface` (`BoardFeltMaterial`, matte felt green).
- **Tests:** `BoardSurfaceSizingTests` felt sizing/Y; `BoardFeltMaterialTests`. No v0.1 tag.

## 2026-08-27 (UTC) — Smooth decorative disk silhouettes

- **`ThinDiskMesh`:** 64-segment disk (mesh radius 0.5, height 2) shared by wood/sea/felt surfaces; existing `Compute*DiskScale` helpers unchanged.
- **`BoardView`:** `BoardSurface`, `WaterSurface`, `FeltSurface` use custom mesh instead of `PrimitiveType.Cylinder` (no collider).
- **Tests:** `ThinDiskMeshTests` (vertex/triangle counts, radius, no NaNs). No v0.1 tag.

## 2026-08-27 (UTC) — Board pick plane at hex top

- **`BoardPickPlane`:** raycast horizontal plane at `BoardView.TileHeight` (fallback y=0); `BoardInputController` uses it for hover/click hit points so angled `TableCamera` aims at visible corners, not pixels below.
- **Tests:** `BoardPickPlaneTests` (EditMode). No v0.1 tag.
