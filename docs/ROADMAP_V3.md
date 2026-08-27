# ROADMAP V3 — efter Fase 2

**Dato:** 2026-08-27 · **Base:** `main` @ 8569fec (PR #52, Fase 2.1–2.6 landet) · **EditMode:** 297/297 grønne.

---

## Hvor vi er

Spillet er et **spilbart prototype**, ikke et shippable produkt. Fase 2 er færdig: save/load (første slice), setup-bonus, largest army, Act 1→3 med kortvækst og dobbelt yield, hybrid nummer-tokens (2–12 + resource rolls + 2d6), og meta med stjerner + unlock-træ i IMGUI. Spilleren mærker en fuld run-loop — leader, draft, shop, kort, events, level-ups, VP-kapløb mod 10 — men UI'en er stadig IMGUI-soup, brættet er cylindre, og balance er nu sim-tuned (89% win/timeout inden dag 20). Meta låser nu uniques og kort-pools bag stjerner; playstyle-unlocks (maps/leaders) uændret.

---

## Backlog (impact × effort)

| Item | Hvorfor det løfter spillet | Impact | Effort | Linux-VM? | Noter |
|------|---------------------------|--------|--------|-----------|-------|
| **Sim-driven day-ceiling / win-rate balance** | Runs skal ende ved 10 VP — ikke ved `--max-days`. Act 2/3 + hybrid tokens gør det værre end før sim-baseline (~790/1000 timeout *før* 2.4–2.6; ikke genmålt). | **H** | **M** | **yes** | `BalanceConfig`, `ActProgression`, evt. AI VP-jagt. Sim-runner + nye metrics. |
| **Autosave + multi-slot + RNG roll-tællere (2.1 rest)** | Spilleren kan pause uden at miste determinisme; QA kan gemme lige før en bug. | **M** | **M** | partial | **Done:** autosave ved `DayPlayerActions`, slot 0/1, legacy `save.json`. RNG roll-tællere udskudt — roll-lister nok til resume. |
| **Meta: lås kort- og unique-*pools* bag stjerner** | Roguelike-identitet: nye runs føles anderledes, ikke kun nye leaders. | **H** | **M** | **yes** | **Done:** 2 free uniques + 7 starter cards; 3 unique unlocks + 2 card packs i `MetaCatalog`. AI bruger fuld `AiPool`. |
| **First-run / onboarding beats (IMGUI)** | Nye spillere forstår hybrid produktion, acts og meta uden wiki. | **H** | **M** | partial | **Done (copy):** `OnboardingCopy` + Tips toggle + hybrid hint; fuld “feel” kræver Play Mode på Mac. |
| **uGUI-erstatning + art pass** | Føles som et spil, ikke et debug-værktøj. | **H** | **H** | **no** | P2 uafgjort. Kræver Mac/Windows til visuel QA; Linux-VM kan skrive prefabs, ikke validere look. |
| **Act 3 indholdsvariation (events/kort)** | Sent game må ikke kun være talinflation (max roll 3, dobbelt dice). | **M** | **M** | **yes** | **Done:** 2 Act 3-only nat-events (`PortBlockade`, `ResourceLevy`); weights act1/2=0. Ingen nyt kort. |
| **PlayMode ud over smoke (playtest harness)** | Scene-boot beviser ikke at man kan spille en run. | **M** | **M** | partial | **Done (Linux smoke + one run):** `GameScenePlayTests` — scripted run select → setup → first day via `GameController` APIs under `xvfb`. Smoke tests bevares. |
| **Windows standalone build** | Distribuerbar `.exe` til playtest uden Unity Editor. | **M** | **L** | **no** | **Blokeret fra Linux-VM** — Windows-Mono tarball 404 (samme klasse som macOS). Se `BLOCKED.md` #3. Byg på Windows-host. |
| **Token/hex visuel pass** | Spilleren ser *hvor* 6/8 sidder — ikke bare IMGUI-labels på cylindre. | **M** | **M** | partial | **Done (placeholder):** cream chip + rim under `TextMesh` i `HexTileView`; hex tiles = `HexPrismMesh` (ikke cylindre); 3D art pack stadig P2. |
| **Forecast / Famine UI-copy fixes** | Forecast ignorerer parameter i UI; Famine-tekst lyver om timing (`TomorrowRolls`). | **L** | **L** | **yes** | **Done:** Forecast card text + no picker; Famine/Good Harvest say "tomorrow". |
| **Sim-runner rapport udvidelse** | Balance-arbejde uden gæt: VP-fordeling, win-rate, act ved afslutning, årsag (`ok`/`max_days`/win). | **M** | **L** | **yes** | Naturlig forløber til balance-pass; ren `tools/`-ændring. |
| **PortDiscount-perk wired** | Perk lover rabat på alle handler ved port — core findes, effekt halv. | **L** | **L** | **yes** | **Done:** `ModifierService.GetShopGiveAmount` + `ShopDealPricing.PerkPortDiscount` (min 2). |

---

## PICK: sim-driven day-ceiling / win-rate balance (follow-up)

**Onboarding beats (IMGUI) landed.** Autosave + multi-slot already done.

- **Pause/resume virker.** Autosave ved nat→dag i slot 0; manuel Save/Load i to slots; legacy `save.json` compat.
- **Onboarding beats** — copy-only fase-bannere + Tips toggle + hybrid hint på dag 1 nat.
- **uGUI + art** forbliver P2 — kræver Mac til visuel QA.

---

## Done recently: PlayMode harness (beyond smoke)

- `GameScenePlayTests` — 2 UnityTests: (1) run select → setup phase after `ConfirmRunSetup`; (2) full setup + skip first night + `EndPlayerDay` → `DayNumber >= 1`, `Winner` null, day/night phase. Drives `GameManager.Controller` public APIs (map/leader/draft, placement, night skip) — no `DebugHooks`, no GUI clicks.
- `GameSceneSmokeTests` unchanged (scene boot + controller after Start).
- Headless: `xvfb-run -a unity test . --mode PlayMode` (licens påkrævet).

---

## Done recently: Act 3 content variation (events)

- **`PortBlockade`** — Act 3-only (weight 0/0/2): random port vertex blockaded for the day; no 2:1/3:1 discount at that harbor; coastal hex overlay (`EventTileOverlayKind.PortBlockade`).
- **`ResourceLevy`** — Act 3-only (weight 0/0/2): human loses 1 of their most abundant resource when the event fires (text-only; cleared with other daily flags).
- Existing 6 events unchanged in Act 3 pool. Save v1 optional `EventBlockedPortVertex`. **333/333** `dotnet test tools/core-tests`. No new card (Act 3 find/shop/level-up is enough).

---

## Done recently: first-run onboarding beats (IMGUI)

- `OnboardingCopy` — pure string helper (`ForPhase`, `TryGetPhaseBanner`, `TryGetFirstNightHybridHint`, `ForGameOver`); EditMode tests.
- `PlaceholderUI` — cyan one-liner per phase; Tips toggle (`PlayerPrefs`, default on); hybrid hint ved dice-linjen dag 1 nat.
- **Forecast / Famine copy:** Forecast card mentions dice reroll; Famine + Good Harvest event text says "tomorrow".
- Tests: **323/323** `dotnet test tools/core-tests`.

---

## Done recently: autosave + multi-slot (2.1 rest)

- Autosave når nat løses til `DayPlayerActions` (`GameController.OnAutosavePoint`).
- Slot 0 = `save.json`, slot 1 = `save_1.json`; `SaveGameSlotStore` path-agnostic til EditMode/`dotnet`.
- IMGUI: Save 1/2, Load 1/2, Autosaved-timestamp. `meta.json` påvirkes ikke.
- RNG roll-tællere **ikke** tilføjet — v1 roll-lister + infereret `MetaStartCardGranted` er nok til resume.
- Tests: 312/312 `dotnet test tools/core-tests`.

---

## Ikke nu

- **v0.1-tag** — P2 (uGUI, art, macOS artifact) og balance er ikke grønne nok til milestone-tag.
- **macOS standalone fra Linux-VM** — Mac-modul 404; se `docs/BLOCKED.md` #2.
- **Windows standalone fra Linux-VM** — Windows-Mono tarball 404; se `docs/BLOCKED.md` #3.
- **Cloud-sync af meta** — `meta.json` er lokal; ingen backend.
- **URP-migration** — Built-in RP er bevidst valg (`docs/DESIGN_RENDERING.md`).

---

## Acceptance (balance-pass)

**Mål:** En typisk AI-vs-human run når **10 VP eller tydeligt nederlag** inden for Act 3's spænd — ikke silent timeout.

### Sim-runner (efter opdatering af metrics + `--max-days` til mindst dag 18–20 for Act 3-runs)

| Metric | Target (udgangspunkt — justér efter første 1000-run baseline) |
|--------|------------------------------------------------------------------|
| Crashes / timeouts | 0 / 0 på 1000 seeds |
| `max_days` (ved `--max-days 20`) | **≤ 25 %** (ned fra ~79 % pre-2.4; genmål efter baseline) |
| Human eller AI win (`status=ok`) | **≥ 50 %** samlet |
| Median dage ved win | **≤ 14** (Act 2–3 sweet spot) |
| VP ved `max_days` | median human VP **≥ 7** (run føles tæt på, ikke stuck på 3) |

### Tests

- [x] Nye/ opdaterede EditMode-tests for ændrede konstanter eller AI VP-adfærd (hvis rørt).
- [x] Eksisterende EditMode-tests stadig grønne (299/299 via `dotnet test tools/core-tests`).
- [x] Sim-runner JSON-output inkluderer: `status`, `winner`, `days`, `playerVp`, `aiVp`, `endAct`
- [x] `docs/IMPLEMENTATION_STATUS.md` + `MISSING_AND_GAPS.md` opdateret med balance-baseline tal efter merge.

### Docs

- [x] Kort afsnit i `docs/TOOLING.md` om anbefalede sim-flag til balance (`--runs 1000 --max-days 20 --map small`). *(done)*
- [x] `docs/DESIGN_NUMBER_TOKENS.md` “Leftover risks” — balance delvist adresseret.

---

## Status: balance-pass **done** (2026-08-27)

200-run sim (`--max-days 20 --map small`): `ok` 89.5%, `max_days` 10.5%, median win day 13, median human VP at `max_days` 7, 0 crash/timeout. See `IMPLEMENTATION_STATUS.md` for before/after table.

*Næste Cursor-opgave: autosave slice, onboarding beats, eller Act 3 indhold per ROADMAP_V3 backlog.*
