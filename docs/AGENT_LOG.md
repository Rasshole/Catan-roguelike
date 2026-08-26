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
