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

