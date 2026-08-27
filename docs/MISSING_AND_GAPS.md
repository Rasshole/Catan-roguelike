# Mangler & ikke-wired endnu

**Næste backlog:** [`docs/ROADMAP_V3.md`](ROADMAP_V3.md)

Sidst opdateret efter Fase 2.6 meta progression på main med 2.5 hybrid tokens, 2.4 Act progression, save/load, setup-bonus og largest army.

Dette er en ærlig statusliste over hvad der **ikke** er færdigt, halvt implementeret, eller kun findes i core uden ordentlig UI/feedback.

---

## Kortstørrelser (nyt)

| Størrelse | Hexes | Vælg |
|-----------|-------|------|
| **Small** | 7 | Startmenu eller `GameManager` default |
| **Medium** | 13 | Kompakt Catan (19 minus 6 hjørner) |
| **Large** | 19 | Klassisk Catan-form |

Der er **in-game map-menu** ved run-start (`RunSelectMap`). Inspector på `GameManager` sætter kun **standard-forhåndsvisning**.

---

## Meta progression (Fase 2.6 — implementeret)

| Emne | Status |
|------|--------|
| **meta.json** | Separat fra `save.json`; run reset sletter ikke meta |
| **Stars** | VP + days/2 + 2 ved sejr; tildeles én gang per run-key |
| **Default** | Small map, Merchant + Pioneer, draft 2 af 2 free uniques (Sawmill + Guild Hall), 7 starter night-draw cards |
| **Unlocks** | Medium/Large map, Warlord/Architect, +1 draft (capped by pool), +1 wheat, start Road Builder, 3 unique unlocks, Sabotage + Market card packs |
| **IMGUI** | Stars + unlock-panel på map-select og game over |
| **AI cards** | Human draw filtered by meta; AI uses full `CardLibrary.AiPool` (+ Act 3 extras) |

**Risiko / mangler:** Ingen cloud-sync; ingen dedikeret “meta hub”-scene (kun IMGUI-panel); card/unique *pool* låst bag stjerner (**done** 2026-08-27).

---

## Bevidst uden for scope (v1)

- **Save / load** — **Fase 2.1 (done):** versioneret JSON (`SaveGame`, format v1); slot 0 = `save.json`, slot 1 = `save_1.json`; autosave ved nat→dag (`DayPlayerActions`); IMGUI Save/Load per slot + Autosaved-label; EditMode round-trip + slot/autosave tests. RNG roll-tællere udskudt (roll-lister i save er nok til resume). **Number tokens + dice** er valgfrie v1-felter.
- **Per-tile nummer-tokens** — **implementeret (Fase 2.5, hybrid b):** klassiske 2–12 per hex + 2d6 + resource rolls; se `docs/DESIGN_NUMBER_TOKENS.md`

---

## Halvt wired — core findes, UI/feedback mangler

### Shop & porte
- **Specifikke 2:1-porte** — wired i `ShopGenerator` + `PortAccess` ✓
- **Generiske 3:1-porte** — `DiscoverPorts` opretter sparse mix (2:1 per ressource + 3:1 generic, skaleret til 7/13/19 hex) ✓
- **PortDiscount-perk** — rabat på alle handler når du kontrollerer en port (ikke kun “unrelated trades”)
- **Risky deal (3. handel)** — core wired; IMGUI shop-knapper viser konsekvens via `ShopDealDisplay` (røver → bedste felt) ✓
- **Effektiv shop-pris** — UI viser kort årsag ved siden af hver handel (`ShopDealPricing`: port 2:1 / port 3:1 / leader / event / perk / base) ✓

### Onboarding (first run)
- **Phase banners** — `OnboardingCopy` one-liners på map, leader, draft, setup, dag 1 nat/dag, game over ✓
- **Hybrid hint** — dag 1 nat ved dice-linjen (token + weather + 2d6) ✓
- **Tips toggle** — `OnboardingTipsStore` via `PlayerPrefs`, default on, uden for save/meta ✓
- **Play Mode feel** — fuld visuel QA kræver stadig Mac (IMGUI copy testbar via EditMode)

### Kort
- Alle **12 kort** har logik i `CardEngine` ✓
- **Bandit Raid** — IMGUI vej-vælger (◀/▶ + label) i nat-fase når kortet er valgt ✓
- **Harbor Charter pending** — cyan statuslinje mens `HarborCharterPending` ✓
- **Embargo-status** — rød statuslinje med ressource + dage tilbage mens aktiv ✓
- **Forecast** — reruller alt inkl. 2d6; kort-tekst og UI (ingen resource-picker) matcher ✓

### Events (~22 % per nat i Act 1; skalerer med act)
- **8 events** i `EventEngine` (6 alle acts + **2 Act 3-only:** Port Blockade, Resource Levy) ✓
- **Board overlays** for storm/famine/gold rush/good harvest/**port blockade** via `EventBoardVisual` + `HexTileView` (market day / bandit raid / resource levy use text or robber only) ✓
- **Famine / Good Harvest** — påvirker `TomorrowRolls`; event-tekst siger "tomorrow" ✓
- Nat-event **BanditRaid** ≠ kort **BanditRaid** (forskellige effekter)

### Leaders & progression
- Leader-vælg + **level-up hver 5. dag** (max 3) ✓
- Level-up **preview** på dagen før (lime statuslinje med seeded perk-valg) + **LevelUpChoice** viser dag/VP/rolls sammen med perk-knapper ✓
- Nogle perks er let wired (Merchant shop, Pioneer free road, Warlord knight)
- **Architect** threshold-rabat: 10 % kun på settlement over threshold-tælleren; Master Builder-rabat kun i `GetEffectiveCost` (0,65 vs 0,75) ✓

### Unique buildings (draft 2 af 5)
- Sawmill, Guild Hall, Monastery, Caravan Post, Fortress Outpost — wired ✓

### AI
- Setup, byg, shop, nat-kort (begrænset pool) ✓
- **Act 2+** — 2 nat-kortspil, smartere kortvalg, flere city-upgrades, stærkere road-blocking ✓
- **Act 3** — ekstra AI-draw + bredere pool (Fertile/Ledger/Forecast) ✓
- **Act 3 events** — Port Blockade (spatial trade-route shutdown) + Resource Levy (economy tax) ✓
- **Embargo** i AI-kortpool; spiller mod mennesket via `PlayerShopEmbargo` (mål = menneskets lager + shop-Give) ✓
- **Harbor Charter** — bevidst **human-only** (`aiCanUse: false`); synergy (+1 VP ved næste kyst-settlement) giver ikke mening for AI uden coastal-prioritet
- Skjult intent — ingen debug-visning
- Under **menneskets Embargo** (`AiShopEmbargo`): springer shop-handler med den blokerede Give-ressource og køber andre tilgængelige handler ✓

### Longest road
- DFS i `RouteCalculator`; ≥5 veje = 2 VP ✓
- **LongRoadBonus** — +1 VP oven i de 2 når human har perk og longest; forsvinder ved tab af longest ✓
- **Modstander-settlement/city splitter ruten** (klassisk Catan) ✓ — egne bygninger splitter ikke
- Lukkede hex-loops tæller alle kanter (komponent med max degree 2); forgreninger bruger længste simple sti ✓

### Largest army
- Tæller spillede Knight-kort (ikke dag-fase robber-flytning) ✓
- 3+ knights og flest = 2 VP via `VictoryCalculator` / `VictoryBreakdown` ✓
- Klassisk Catan tie: første beholder indtil nogen **overgår** (lige antal stjæler ikke) ✓
- AI foretrækker Knight når tæt på threshold eller når human holder og AI kan overhale (`AiController.PickNightCard`) ✓

### Robber
- Tile-block + steal på dag-flytning og Knight ✓
- Offer vælges blandt spillere med bygning på det blokerede hex (seedet RNG)

---

## Unity / UX / polish

| Emne | Status |
|------|--------|
| **IMGUI** (`PlaceholderUI`) | Fungerer, men er placeholder — ikke uGUI/UI Toolkit |
| **3D-art** | Cylindre/kuber — ikke bordspils-look |
| **Game.unity** | Committed (`Assets/_Project/Scenes/Game.unity`) |
| **Render pipeline** | **Built-in RP** (beslutning 0.5). Ingen URP-pakke. Se `docs/DESIGN_RENDERING.md` |
| **Map size** | Startmenu + inspector default |
| **VP-breakdown** | IMGUI viser total + én linje per spiller (settlements / cities / longest / long road / largest army / bonus) via `VictoryBreakdown` |
| **Pending effects** | Road Builder / Master Builder — minimal feedback |
| **Game over** | IMGUI run-summary (VP breakdown, seed, day, map, leader) + stars earned / unlock shop + scene reload ✓ |
| **README** | Opdateret til fresh-clone flow (Game.unity, macOS Editor, playflow) ✓ |

---

## Test-dækning

Eksisterende EditMode-tests:
- `NumberTokenTests` / `HybridProductionTests` / `DiceRollEngineTests` / `AiTokenStrategyTests` — token assignment (7/13/19, expansion, red adjacency), desert, hybrid production, Act 2 double dice, save round-trip tokens, BanditRaid pip targeting
- `RollEngineTests` — roll caps
- `PlacementValidatorTests` — distance rule
- `ProductionCalculatorTests` — multi-hex production
- `PortAccessTests` — specifik 2:1, generisk 3:1, 2:1 vs 3:1 prioritet, base rate, sparse discovery på 7/13/19
- `MapPresetsTests` — 7/13/19 tile counts
- `VictoryCalculatorTests` — Harbor Charter + FirstCityVp overlever `RefreshVictoryPoints`; LongRoadBonus +1 / mister longest / ingen double-count; `VictoryBreakdown` dele summer til total
- `ArmyCalculatorTests` / `LargestArmyVictoryTests` — grant ved 3; tie beholder holder; overtake stjæler; tab ved overgang; breakdown inkl. army; failed Knight tæller ikke
- `AiLargestArmyStrategyTests` — AI foretrækker Knight ved threshold-1 og når human holder army og AI kan overhale
- `VertexGraphTests` — Canonicalize idempotent; VertexDistance terminerer med buildings
- `RouteCalculatorTests` — længde N, enemy split, own settlement splitter ikke, tom=0, disjoint/ties, VertexDistance-regression med buildings, **disabled roads (Bandit Raid / `DisabledRoads`)**, **hex-loop counts all 6 edges**, **forgrening**, **`GetLongestRoadOwner` tie / threshold / disabled flip**
- `GameControllerSetupTests` — AI setup places 2 settlements + 2 roads
- `SetupBonusTests` — 2. settlement (spiller + AI) giver startressourcer fra tilstødende tiles; 1. settlement giver ikke; desert/`IsDesert` springes over
- `RobberStealTests` — day-move steal, knight steal, no victim, seeded RNG
- `AiControllerShopTests` — AI shop køber ved rigtig afford-check; springer over når den ikke har råd
- `AiEmbargoStrategyTests` — `AiPool` indeholder Embargo (ikke Harbor Charter); AI draw; human Embargo → `AiShopEmbargo`; AI Embargo → `PlayerShopEmbargo` på strategisk ressource; shop skip under embargo; human shop blokeret af AI Embargo
- `ShopGeneratorRiskyDealTests` — risky deal flytter robber til købers bedste tile (human + AI); RiskyDealsSafe skipper kun for human
- `ShopGeneratorTests` — seeded `GenerateDailyDeals` (3 handler, 3. risky 2:1, ExtraShopDeal perk); safe `TryPurchase`; embargo blokerer Give for human (`PlayerShopEmbargo`) og AI (`AiShopEmbargo`) via `GetEffectiveGiveAmount` / `TryPurchase`; MarketDay `EventShopBonus` → 3:1 via `ShopGenerator` (ikke kun `ModifierService` / `ShopDealPricing`)
- `ModifierServiceNightUniquesTests` — Monastery laveste roll + tie-break; RollInsurance scarcest inventory; once-per-run; begge samme nat
- `BanditRaidTests` — `OpponentRoadSelector` stabil sortering/index; `ApplyBanditRaid` disabler valgt kant (ikke en anden); fejler rent uden modstander-veje
- `PendingStatusDisplayTests` — Harbor Charter / Embargo / level-up preview statuslinjer (skjult når inaktiv)
- `ShopDealPricingTests` — pris-årsag (base, port 2:1/3:1, leader, event, perk); matcher `ShopGenerator.GetEffectiveGiveAmount`
- `ShopDealDisplayTests` — risky shop-knap label med robber-konsekvens; RiskyDealsSafe viser waived-tekst
- `ArchitectCostModifierTests` — Architect 10 % kun threshold-settlement; road/city/non-threshold fuld pris; Master Builder 0,65 vs 0,75 uden dobbeltrabat
- `RunProgressionTests` — `WillOfferLevelUpAfterThisDay` (dag 4); `ShouldOfferLevelUp` (dag 5); max 3; `LastLevelUpDay`; seeded preview = offer
- `RunProgressionDraftFlowTests` — pre-game draft via `GameController`: `RunSelectMap` start; `SelectMap` (7/13/19 + ports); phase guards; `SelectLeader` → draft status; `ToggleDraftUnique` add/remove/cap at `DraftPickCount` (alle 5 uniques); invalid `ConfirmRunSetup` (0–1 picks / forkert fase); valid confirm → `RunSetupComplete` + AI setup → `SetupPlayerSettlement1`
- `CardEngineTests` — alle 12 kort via `PlayCard` / `DrawCard` / `DrawToHand`: roll-manipulation (Ledger, Drought, Fertile, Forecast seeded), Year of Plenty, Monopoly (half / MonopolyFull / zero stock), Road Builder + Master Builder pending, Harbor Charter, Embargo fail + EmbargoExtended; Knight invalid target + KnightMovesRobberTwice; Bandit Raid på egen vej fejler; hand/max-size / not-in-hand
- `EventEngineTests` — alle 6 events (effekt + besked); nat apply / dag clear timing; seeded `MaybeRollEvent`; `BeginNight` Good Harvest rolls
- `RunSummaryDisplayTests` — game-over summary lines (human vs AI win wording; seed/day/map/leader; VP breakdown; safe when no winner)
- `EventBoardVisualTests` — tile overlays: none; storm on `EventStormTile`; famine wheat; gold rush stone; good harvest all tiles; market day / bandit raid none
- `GameControllerIntegrationTests` — seeded setup → nat/dag-cyklus uden hang; `SkipNightCard` / `PlayPlayerCard` → `DayPlayerActions`; win ved 10 VP; level-up på dag 5 via `EndPlayerDay`; disabled roads + event-flags ryddes ved daggrænse
- `MetaProgressionTests` — star award formula; fresh defaults; pre-game flow uden køb; purchase; run award dedup; meta/run save isolation; start wheat/card; extra draft; map gating
- `GameSceneSmokeTests` (PlayMode) — `Game.unity` loader; `GameManager` / `BoardView` / `PlaceholderUI` findes; efter Start har `GameManager.Controller` + state i run-select eller setup-fase (ingen umiddelbar NRE)
- `ActProgressionTests` — dag→act thresholds, yield/event/AI/map knobs
- `MapExpansionTests` — `ExpandBoard` tile counts, buildings preserved, coastal flags
- `ActProgressionFlowTests` — combined rolls Act 2/3, event chance/weights, AI double-play, dag 6 map growth

**Mangler tests for:**
- PlayMode / UI-tests ud over scene-boot smoke (fx fuld IMGUI-interaktion)

`AiController` nat/dag-adfærd er dækket af `AiControllerShopTests` (shop afford) og `AiEmbargoStrategyTests` (embargo skip, `ExecuteNightPlan` Embargo mod spiller inkl. `PlayerShopEmbargo` + blokeret human shop).

---

## Anbefalet rækkefølge næste gang

Se [`docs/ROADMAP_V3.md`](ROADMAP_V3.md) for fuld backlog. **Valgt næste:**

1. ~~**Sim-driven day-ceiling / win-rate balance**~~ — **addressed** (2026-08-27): `BalanceConfig` + hybrid production floor + act pacing + sim-driver heuristics; 200-run baseline in `IMPLEMENTATION_STATUS.md`.
2. ~~Sim-runner metrics~~ — done (`endAct`, summary `max_days`); use `--max-days 20` for measurement
3. Derefter: meta pool-locks, onboarding, save 2.1-rest — se roadmap

---

## Hurtig reference — filer

```
Core/Data/MapPresets.cs     — 7 / 13 / 19 hex presets
Core/Data/MapSize.cs        — Small=7, Medium=13, Large=19
Game/GameManager.cs         — mapSize inspector
Game/BoardView.cs           — board scale efter tile count
Game/MetaProgressionFile.cs  — meta.json IO (persistentDataPath)
Core/Progression/MetaProgression.cs — stars, unlocks, award/purchase
Core/Progression/MetaCatalog.cs   — unlock costs + descriptions
Game/PlaceholderUI.cs       — al UI (IMGUI); …; game-over run-summary + meta unlock shop
Core/Shop/ShopDealDisplay.cs — shop-knap labels + risky robber-konsekvens-tekst
Core/RunSummaryDisplay.cs — rene game-over linjer (winner, dag, kort, leader, seed, VP-breakdown)
Core/Victory/VictoryBreakdown.cs — VP-dele per spiller (settlements, cities, longest, long road, largest army, bonus)
Core/Victory/ArmyCalculator.cs — knight counts + Largest Army owner (classic tie rules)
Core/PendingStatusDisplay.cs — rene statuslinjer for Harbor Charter / Embargo / level-up preview
Core/Cards/EmbargoTargetSelector.cs — AI Embargo-mål (spiller-lager + shop Give)
Core/Progression/ActProgression.cs — day→act mapping, yield/event/AI/map scaling (Fase 2.4)
Core/Progression/RunProgression.cs — level-up interval, `WillOfferLevelUpAfterThisDay`, seeded perk draft
Core/Shop/ShopDealPricing.cs — klassificerer effektiv shop-pris (port / leader / event / base)
Core/Events/EventBoardVisual.cs — tile overlay kind per hex for active night event
Core/Map/OpponentRoadSelector.cs — stabil liste + index for modstander-veje
docs/IMPLEMENTATION_STATUS.md — kortere checkliste
```
