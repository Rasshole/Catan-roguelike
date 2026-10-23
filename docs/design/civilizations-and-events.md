# Civilizations and events (v131)

These two roguelike layers sit on top of the run setup (map, leader, unique draft) and the day/night loop.
They are data-driven:

- **Civilizations:** `Assets/_Project/Scripts/Core/Civilizations/`
  - `CivilizationLibrary` holds the text and art keys.
  - `CivilizationRules` holds every rule hook.
- **Events:** `Assets/_Project/Scripts/Core/Events/EventEngine.cs`
  - `EventLibrary.Pool` is an ordered list of `EventDefinition` entries. Each entry has a name, a description, a category, per-act weights, an effect delegate, and an optional availability predicate.
  - `EventEffects` holds the effect code.

Painted art lives in `Assets/_Project/Resources/Art/{Civilizations,Events,Cards}`. It is built by
`tools/art/make_emblems.py` from `tools/art/emblems-src` and `tools/art/card-frames-src`.

## Civilizations

### How the pick works

1. After the unique draft, `ConfirmRunSetup` deals an offer of **3 distinct civilizations**. The offer comes from `CivilizationOffer.Generate(runSeed)`, a seeded Fisher-Yates shuffle, so the same run seed always gets the same offer.
2. The game enters `GamePhase.RunSelectCivilization`.
3. The HUD shows three painted cards. Each card has an emblem, a name, a tagline, the passive (green) and the drawback (red).
4. `GameController.ChooseCivilization(id)` does the rest:
   - It applies any start bonus.
   - It logs "You lead X: passive".
   - It starts board setup.

The chosen civilization appears in the "You" player panel as its emblem plus a short name.

### Scope and saves

- Civilizations only affect the human player. The AI never has one.
- The pick is enabled through `GameController.CivilizationPickEnabled`. `GameManager` turns it on. Headless tests and old flows keep the legacy setup path (no civilization).
- The `Civilization` and `CivilizationOffer` fields are saved.

### The civilizations

| Civilization | Passive | Drawback | Hook |
|---|---|---|---|
| **The Sea Folk** | +1 fish whenever your fishing grounds catch anything. Every fish-shop action costs 1 fish less (minimum 1). | none | `FishCatchBonus`, `FishCost` |
| **The Forest Folk** | When a 2 or 12 is rolled, gain 1 Lumber per forest you border (at least 1). | none | `DiceBonus` |
| **Merchant Republic** | Bank trades are 3:1 everywhere. 2:1 ports still apply. | Whenever a 7 is rolled, lose 1 random resource (pirate tariff, added in v132). | `BaseBankRate`, `OnSevenRolled` |
| **Steppe Nomads** | Settlements cost no Brick. | Cities cost 1 more Ore. | `ApplyCostModifiers` |
| **Highland Clans** | Mountains yield +1 Ore per hit. | Pastures yield 1 less Wool per hit (floored at 0). | `HexYieldDeltaPerHit` |
| **Shepherd Kingdom** | Start with 2 extra Wool. Pastures yield +1 Wool per hit. | Cities cost 1 more Wheat. | `ApplyStartBonus`, `HexYieldDeltaPerHit`, `ApplyCostModifiers` |
| **The Brickwrights** | Roads cost no Lumber. Whenever a 7 is rolled, gain 1 Brick. | none | `ApplyCostModifiers`, `OnSevenRolled` |
| **The Academy** | Draw 1 extra card every night. | Settlements cost 1 more Wheat. | `ExtraNightDraws`, `ApplyCostModifiers` |
| **Northern Raiders** | Whenever a 7 is rolled, steal 1 random resource from the opponent. | Bank trades without a port are 5:1. | `OnSevenRolled`, `BaseBankRate` |
| **Sun Kingdom** | The robber never blocks your production. | none | `IgnoresRobber` (through `ModifierService.IsRobberBlocking`) |

### Where the hooks are wired

- **Build costs:** `ModifierService.ApplyLeaderCostModifiers` ends in `CivilizationRules.ApplyCostModifiers`.
- **Bank rate:** `BankTradeCalculator.GetGiveRateForResource` uses the civilization base rate when you have no port.
- **Production:** `ProductionCalculator` applies the per-hit deltas.
- **Night, sevens and fishing:** `GameController` handles the dice bonus, the 7-roll effects, the extra night draws and the fish catch bonus.
- **Fish shop:** `FishermenFishActions` and the fish panel use `CivilizationRules.FishCost`, so the displayed costs follow the civilization.

## Events

### Timing

Events are on a **fixed cadence** instead of a random chance per night:

- Act 1 and Act 2: every **3rd** day.
- Act 3: every **2nd** day.

The rules are in `EventEngine.IsEventDay` and `DaysUntilNextEvent`. On an event night, `RollNightEvent` makes a weighted pick by act. `PickWeightedEvent` skips events whose availability predicate fails. For example, Squall needs the Fishermen variant, and Earthquake needs at least two numbered tiles.

The pick uses the `EventEngine` RNG, which is seeded from the run seed, so it is deterministic. The legacy `MaybeRollEvent` (chance, then pick) is kept for tests and tools.

### Lifetime

- **Daily effects** last for the following production day. `ClearDailyEventEffects` clears them at the end of the player day.
- **Permanent effects** stay for the rest of the run. These are token swaps and shifts, robber moves, cards and resources.

### Announcement

The HUD shows a wide painted event card during the night (and before the morning roll). The card has:

- the category emblem;
- a kicker line ("EVENT · DAY n · CATEGORY");
- the event name and description;
- a concrete detail line, for example "The 6 on Hills swapped with the 8 on Pasture.";
- a "Next event in N days" hint.

Clicking the card dismisses it. The tiles the event touched are tinted on the board through `EventBoardVisual`.

### The events

The weights column gives the weight in Act 1 / Act 2 / Act 3. A weight of 0 means the event cannot appear in that act.

| Event | Category | Weights (A1/A2/A3) | Effect | Lasts |
|---|---|---|---|---|
| Storm | Hardship | 1/2/3 | One random tile produces nothing today. | day |
| Famine | Hardship | 1/2/3 | Wheat rolls are capped at 1 tomorrow. | day |
| Gold Rush | Bounty | 1/1/2 | Ore production is doubled today. | day |
| Market Day | Trade | 1/1/1 | All shop trades are 3:1 today. | day |
| Good Harvest | Bounty | 1/1/1 | All resource rolls +1 tomorrow (respects the cap). | day |
| Bandit Raid | Robber | 1/2/3 | The robber moves to the tile where you produce the most. | permanent |
| Port Blockade | Sea | 0/0/2 | One port gives no discount today. | day |
| Resource Levy | Hardship | 0/0/2 | The crown takes 1 of your most abundant resource. | permanent |
| **Earthquake** | Board | 1/1/2 | Two number tokens (preferably different numbers) swap places. | permanent |
| **Shifting Fortunes** | Board | 1/1/1 | One number token shifts by ±1. It never lands on 7: it hops over 7, 2 only goes up and 12 only goes down. | permanent |
| **Double Harvest** | Bounty | 1/1/1 | All production is doubled today (`EventProductionPercent = 200`). | day |
| **Lean Times** | Hardship | 1/1/2 | All production is halved today, rounded down. | day |
| **Royal Patronage** | Gift | 1/1/1 | Every player draws a card. | permanent |
| **Harvest Festival** | Gift | 2/1/1 | Every player gets 1 of the same random resource. | permanent |
| **Blight** | Hardship | 1/1/2 | One random resource produces nothing today. | day |
| **Squall** | Sea | 1/1/1 | No fish are caught today. Only appears when Fishermen is enabled. | day |
| **Bountiful Land** | Bounty | 1/1/1 | One random terrain gives +1 per hit today. | day |
| **Restless Robber** | Robber | 1/1/1 | The robber moves to a random land tile. | permanent |

Events in **bold** are new in v131.

### Production order

Production for the human player is calculated in this order:

1. Base yield per hit.
2. The civilization delta per hit, floored at 0.
3. +1 per hit for Bountiful Land on its terrain.
4. Storm, Gold Rush and the robber (Sun Kingdom ignores the robber).
5. The civilization dice bonus (Forest Folk).
6. `ApplyEventModifiers`: the Blight resource is zeroed, then the production percent is applied.

### Adding an event

1. Append a value to `EventId`. Saves store enums as ints, so never reorder existing values.
2. Add an `EventDefinition` to `EventLibrary.Pool`. Appending keeps the existing seeded picks stable.
3. Put any non-trivial effect in `EventEffects`. Write the human-readable `EventDetail` there.
4. If the effect needs a new daily flag, add it to `GameState`, reset it in `ClearDailyEventEffects` and in `ResetForNewMap`, and save it as nullable so old saves still load.
5. Add tests in `Assets/Tests/EditMode/CivilizationAndEventV131Tests.cs`.

## Saves

New fields: `Civilization`, `CivilizationOffer`, `EventProductionPercent`, `EventBlockedResource`, `EventBonusResource`, `EventFishStorm`, `EventDetail`, `EventDay`.

Old saves without these fields load with these defaults:

- `Civilization`: none.
- `EventProductionPercent`: 100.
- `EventDay`: -1.
