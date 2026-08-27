# Mangler & ikke-wired endnu

Sidst opdateret efter P3 RunProgression draft-flow EditMode tests (map → leader → unique draft → setup).

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

## Bevidst uden for scope (v1)

- **Meta progression** mellem runs (permanente unlocks)
- **Save / load**
- **Act 2** progression (flere yield-rolls, større kort over tid)
- **Largest army** som VP-kilde
- **Per-tile nummer-tokens** (klassisk Catan 2–12) — produktion bruger abstrakte daglige rolls per ressource
- **Setup-bonus** fra 2. settlement (startressourcer fra tilstødende tiles)

---

## Halvt wired — core findes, UI/feedback mangler

### Shop & porte
- **Specifikke 2:1-porte** — wired i `ShopGenerator` + `PortAccess` ✓
- **Generiske 3:1-porte** — `DiscoverPorts` opretter sparse mix (2:1 per ressource + 3:1 generic, skaleret til 7/13/19 hex) ✓
- **PortDiscount-perk** — rabat på alle handler når du kontrollerer en port (ikke kun “unrelated trades”)
- **Risky deal (3. handel)** — core wired; UI forklarer ikke konsekvensen tydeligt
- **Effektiv shop-pris** — UI viser kort årsag ved siden af hver handel (`ShopDealPricing`: port 2:1 / port 3:1 / leader / event / perk / base) ✓

### Kort
- Alle **12 kort** har logik i `CardEngine` ✓
- **Bandit Raid** — IMGUI vej-vælger (◀/▶ + label) i nat-fase når kortet er valgt ✓
- **Harbor Charter pending** — cyan statuslinje mens `HarborCharterPending` ✓
- **Embargo-status** — rød statuslinje med ressource + dage tilbage mens aktiv ✓
- **Forecast** — reruller alt (korrekt), men parameter ignoreres i UI

### Events (~22 % per nat)
- 6 events i `EventEngine` ✓
- Kun **tekstlinje** i UI — ingen visuel storm/famine på brættet
- **Famine** påvirker `TomorrowRolls`; UI-tekst kan være misvisende om timing
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
- **Embargo** i AI-kortpool; spiller mod mennesket via `PlayerShopEmbargo` (mål = menneskets lager + shop-Give) ✓
- **Harbor Charter** — bevidst **human-only** (`aiCanUse: false`); synergy (+1 VP ved næste kyst-settlement) giver ikke mening for AI uden coastal-prioritet
- Skjult intent — ingen debug-visning
- Under **menneskets Embargo** (`AiShopEmbargo`): springer shop-handler med den blokerede Give-ressource og køber andre tilgængelige handler ✓

### Longest road
- DFS i `RouteCalculator`; ≥5 veje = 2 VP ✓
- **LongRoadBonus** — +1 VP oven i de 2 når human har perk og longest; forsvinder ved tab af longest ✓
- **Modstander-settlement/city splitter ruten** (klassisk Catan) ✓ — egne bygninger splitter ikke
- Edge cases på forgreninger/loops kan stadig være forkerte (vertex-DFS, ikke edge-trail)

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
| **VP-breakdown** | IMGUI viser total + én linje per spiller (settlements / cities / longest / long road / bonus) via `VictoryBreakdown` |
| **Pending effects** | Road Builder / Master Builder — minimal feedback |
| **Game over** | Kun scene reload — ingen run-summary |
| **README** | Kan være bagud ift. leaders/draft/klik-placering |

### Døde / ubrugte definitioner
- `GamePhase.DayEndCheck` og `DaySubPhase` enum — defineret, aldrig brugt

---

## Test-dækning

Eksisterende EditMode-tests:
- `RollEngineTests` — roll caps
- `PlacementValidatorTests` — distance rule
- `ProductionCalculatorTests` — multi-hex production
- `PortAccessTests` — specifik 2:1, generisk 3:1, 2:1 vs 3:1 prioritet, base rate, sparse discovery på 7/13/19
- `MapPresetsTests` — 7/13/19 tile counts
- `VictoryCalculatorTests` — Harbor Charter + FirstCityVp overlever `RefreshVictoryPoints`; LongRoadBonus +1 / mister longest / ingen double-count; `VictoryBreakdown` dele summer til total
- `VertexGraphTests` — Canonicalize idempotent; VertexDistance terminerer med buildings
- `RouteCalculatorTests` — længde N, enemy split, own settlement splitter ikke, tom=0, disjoint/ties, VertexDistance-regression med buildings, **disabled roads (Bandit Raid / `DisabledRoads`)**, **hex-loop vertex-DFS (6→5)**, **forgrening**, **`GetLongestRoadOwner` tie / threshold / disabled flip**
- `GameControllerSetupTests` — AI setup places 2 settlements + 2 roads
- `RobberStealTests` — day-move steal, knight steal, no victim, seeded RNG
- `AiControllerShopTests` — AI shop køber ved rigtig afford-check; springer over når den ikke har råd
- `AiEmbargoStrategyTests` — `AiPool` indeholder Embargo (ikke Harbor Charter); AI draw; human Embargo → `AiShopEmbargo`; AI Embargo → `PlayerShopEmbargo` på strategisk ressource; shop skip under embargo; human shop blokeret af AI Embargo
- `ShopGeneratorRiskyDealTests` — risky deal flytter robber til købers bedste tile (human + AI); RiskyDealsSafe skipper kun for human
- `ShopGeneratorTests` — seeded `GenerateDailyDeals` (3 handler, 3. risky 2:1, ExtraShopDeal perk); safe `TryPurchase`; embargo blokerer Give for human (`PlayerShopEmbargo`) og AI (`AiShopEmbargo`) via `GetEffectiveGiveAmount` / `TryPurchase`; MarketDay `EventShopBonus` → 3:1 via `ShopGenerator` (ikke kun `ModifierService` / `ShopDealPricing`)
- `ModifierServiceNightUniquesTests` — Monastery laveste roll + tie-break; RollInsurance scarcest inventory; once-per-run; begge samme nat
- `BanditRaidTests` — `OpponentRoadSelector` stabil sortering/index; `ApplyBanditRaid` disabler valgt kant (ikke en anden); fejler rent uden modstander-veje
- `PendingStatusDisplayTests` — Harbor Charter / Embargo / level-up preview statuslinjer (skjult når inaktiv)
- `ShopDealPricingTests` — pris-årsag (base, port 2:1/3:1, leader, event, perk); matcher `ShopGenerator.GetEffectiveGiveAmount`
- `ArchitectCostModifierTests` — Architect 10 % kun threshold-settlement; road/city/non-threshold fuld pris; Master Builder 0,65 vs 0,75 uden dobbeltrabat
- `RunProgressionTests` — `WillOfferLevelUpAfterThisDay` (dag 4); `ShouldOfferLevelUp` (dag 5); max 3; `LastLevelUpDay`; seeded preview = offer
- `RunProgressionDraftFlowTests` — pre-game draft via `GameController`: `RunSelectMap` start; `SelectMap` (7/13/19 + ports); phase guards; `SelectLeader` → draft status; `ToggleDraftUnique` add/remove/cap at `DraftPickCount` (alle 5 uniques); invalid `ConfirmRunSetup` (0–1 picks / forkert fase); valid confirm → `RunSetupComplete` + AI setup → `SetupPlayerSettlement1`
- `CardEngineTests` — alle 12 kort via `PlayCard` / `DrawCard` / `DrawToHand`: roll-manipulation (Ledger, Drought, Fertile, Forecast seeded), Year of Plenty, Monopoly (half / MonopolyFull / zero stock), Road Builder + Master Builder pending, Harbor Charter, Embargo fail + EmbargoExtended; Knight invalid target + KnightMovesRobberTwice; Bandit Raid på egen vej fejler; hand/max-size / not-in-hand
- `EventEngineTests` — alle 6 events (effekt + besked); nat apply / dag clear timing; seeded `MaybeRollEvent`; `BeginNight` Good Harvest rolls
- `GameControllerIntegrationTests` — seeded setup → nat/dag-cyklus uden hang; `SkipNightCard` / `PlayPlayerCard` → `DayPlayerActions`; win ved 10 VP; level-up på dag 5 via `EndPlayerDay`; disabled roads + event-flags ryddes ved daggrænse

**Mangler tests for:**
- PlayMode / UI-tests

`AiController` nat/dag-adfærd er dækket af `AiControllerShopTests` (shop afford) og `AiEmbargoStrategyTests` (embargo skip, `ExecuteNightPlan` Embargo mod spiller inkl. `PlayerShopEmbargo` + blokeret human shop).

---

## Anbefalet rækkefølge næste gang

1. **Risky deal** tydeligere konsekvens-tekst i shop-knapper
2. Longest road: bedre graf-algoritme (loops / forgreninger)
3. Rig UI (uGUI) + art pass
4. Integrationstests + playtest på 19-hex

---

## Hurtig reference — filer

```
Core/Data/MapPresets.cs     — 7 / 13 / 19 hex presets
Core/Data/MapSize.cs        — Small=7, Medium=13, Large=19
Game/GameManager.cs         — mapSize inspector
Game/BoardView.cs           — board scale efter tile count
Game/PlaceholderUI.cs       — al UI (IMGUI); Bandit Raid road picker; Harbor Charter + Embargo + level-up preview; shop-pris årsag; VP-breakdown; LevelUpChoice med fuld HUD
Core/Victory/VictoryBreakdown.cs — VP-dele per spiller (settlements, cities, longest, long road, bonus)
Core/PendingStatusDisplay.cs — rene statuslinjer for Harbor Charter / Embargo / level-up preview
Core/Cards/EmbargoTargetSelector.cs — AI Embargo-mål (spiller-lager + shop Give)
Core/Progression/RunProgression.cs — level-up interval, `WillOfferLevelUpAfterThisDay`, seeded perk draft
Core/Shop/ShopDealPricing.cs — klassificerer effektiv shop-pris (port / leader / event / base)
Core/Map/OpponentRoadSelector.cs — stabil liste + index for modstander-veje
docs/IMPLEMENTATION_STATUS.md — kortere checkliste
```
