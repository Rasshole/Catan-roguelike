# Mangler & ikke-wired endnu

Sidst opdateret efter tilføjelse af **7 / 13 / 19-hex** kort.

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

## Bugs / logik der ikke matcher design

| Problem | Hvor | Note |
|---------|------|------|
| **Bonus-VP forsvinder** | `VictoryCalculator.RefreshVictoryPoints` | Harbor Charter (+1 VP) og FirstCityVp tilføjes via `AddVictoryPoints`, men overskrives ved refresh (kun bygninger + longest road tælles) |
| **LongRoadBonus-perk** | `LeaderLibrary` / `VictoryCalculator` | Defineret (“+1 VP ved longest route”) men implementeret ingen steder |
| **Monastery** | `ModifierService` | Beskrivelse: laveste roll; kode: laveste enum-rækkefølge |
| **RollInsurance** | `ModifierService` | Beskrivelse: mest knappe ressource; kode: første 0-roll |
| **AI risky shop** | `ShopGenerator.ApplyRiskyDealPenalty` | Straf (robber) gælder kun menneskespiller |
| **AI shop check** | `AiController.TryShopPurchases` | `CanAfford(empty bundle)` er no-op før rigtig check |
| **StealRandomResource** | `GameController` | Bruger `new Random()` uden seed |

---

## Halvt wired — core findes, UI/feedback mangler

### Shop & porte
- **Specifikke 2:1-porte** — wired i `ShopGenerator` + `PortAccess` ✓
- **Generiske 3:1-porte** — API findes (`PortDefinition.IsGeneric`, `HasGenericPort`), men `DiscoverPorts` opretter **kun** ressource-specifikke porte
- **PortDiscount-perk** — rabat på alle handler når du kontrollerer en port (ikke kun “unrelated trades”)
- **Risky deal (3. handel)** — core wired; UI forklarer ikke konsekvensen tydeligt
- **Effektiv shop-pris** — beregnes, men UI viser ikke *hvorfor* (port / leader / event)

### Kort
- Alle **12 kort** har logik i `CardEngine` ✓
- **Bandit Raid** — ingen UI til at vælge modstanderens vej (`_selectedRoadIndex` bruges ikke)
- **Harbor Charter pending** — vises ikke i UI
- **Embargo-status** — `AiShopEmbargo` / `AiEmbargoDaysLeft` vises ikke
- **Forecast** — reruller alt (korrekt), men parameter ignoreres i UI

### Events (~22 % per nat)
- 6 events i `EventEngine` ✓
- Kun **tekstlinje** i UI — ingen visuel storm/famine på brættet
- **Famine** påvirker `TomorrowRolls`; UI-tekst kan være misvisende om timing
- Nat-event **BanditRaid** ≠ kort **BanditRaid** (forskellige effekter)

### Leaders & progression
- Leader-vælg + **level-up hver 5. dag** (max 3) ✓
- Level-up **afbryder** dags-flow uden forhåndsvisning
- Nogle perks er let wired (Merchant shop, Pioneer free road, Warlord knight)
- **Architect** threshold-rabat er generel 10 %, ikke eksplicit threshold-only

### Unique buildings (draft 2 af 5)
- Sawmill, Guild Hall, Caravan Post, Fortress Outpost — wired ✓
- **Monastery** — auto-trigger én gang, spiller vælger ikke

### AI
- Setup, byg, shop, nat-kort (begrænset pool) ✓
- **Embargo** og **Harbor Charter** ikke i AI-kortpool
- Skjult intent — ingen debug-visning
- Reagerer ikke strategisk på embargo

### Longest road
- DFS i `RouteCalculator`; ≥5 veje = 2 VP ✓
- **Modstander-settlements blokerer ikke** ruter (klassisk Catan-regel mangler)
- Edge cases på forgreninger kan være forkerte

### Robber
- Tile-block + Knight-stjæl ✓
- Dag-flytning: hex-vælger i UI, men **ingen steal** (`steal: false` hardcoded) — kun Knight stjæler

---

## Unity / UX / polish

| Emne | Status |
|------|--------|
| **IMGUI** (`PlaceholderUI`) | Fungerer, men er placeholder — ikke uGUI/UI Toolkit |
| **3D-art** | Cylindre/kuber — ikke bordspils-look |
| **Game.unity** | Ikke i git — kør **Catan Roguelike → Setup Game Scene** |
| **Map size** | Startmenu + inspector default |
| **VP-breakdown** | Kun total VP — ingen opdeling (bygninger / longest / bonus) |
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
- `PortAccessTests` — specifik 2:1 port
- `MapPresetsTests` — 7/13/19 tile counts

**Mangler tests for:**
- `VictoryCalculator` / bonus-VP-bug
- `RouteCalculator` (longest road, ties, disabled roads)
- `EventEngine` (alle events, timing)
- `CardEngine` (alle 12 kort)
- `ShopGenerator` (risky deals, embargo, MarketDay)
- `ModifierService` (leaders, uniques, perks)
- `RunProgression` / draft
- `AiController`
- Fuld `GameController` integration (dag/nat-cyklus, win)
- PlayMode / UI-tests

---

## Anbefalet rækkefølge næste gang

1. Fix **bonus-VP refresh**-bug (Harbor Charter, FirstCityVp, LongRoadBonus)
2. **Bandit Raid** vej-vælger i UI
3. **Generiske 3:1-porte** på kystvertices
4. Longest road: **modstander-blokering** + bedre graf-algoritme
5. Rig UI (uGUI) + committed `Game.unity`
6. Integrationstests + playtest på 19-hex

---

## Hurtig reference — filer

```
Core/Data/MapPresets.cs     — 7 / 13 / 19 hex presets
Core/Data/MapSize.cs        — Small=7, Medium=13, Large=19
Game/GameManager.cs         — mapSize inspector
Game/BoardView.cs           — board scale efter tile count
Game/PlaceholderUI.cs       — al UI (IMGUI)
docs/IMPLEMENTATION_STATUS.md — kortere checkliste
```
