# Design: Per-Tile Number Tokens (Fase 2.5)

**Status:** Chosen model **(b) Hybrid** — implemented on branch `cursor/number-tokens-2-5-7a97`.

Classic Catan places number tokens 2–12 on resource hexes (no 7; desert has none). Production today uses abstract per-resource nightly rolls (0/1/2, Act 3 max 3) multiplied across all adjacent hexes of that resource. Tokens are a **replacement of the production trigger model**, not a cosmetic overlay.

---

## Option (a) — Replace roll system entirely

**Model:** Remove per-resource `TodayRolls`. Each night roll 2d6 (Act 2: two independent 2d6 sums). A hex produces 1 (settlement) or 2 (city) when the dice sum equals its token. Cards/events that tweak “rolls” must be rewritten as dice modifiers or removed.

| Area | Impact |
|------|--------|
| **Leaders** | RollInsurance / Monastery become dice-focused or obsolete |
| **Cards** | Drought, Fertile Season, Ledger, Forecast need full redesign |
| **Events** | Famine, GoodHarvest lose meaning unless re-mapped to dice |
| **Shop / setup-bonus** | Unaffected |
| **AI** | Placement scores token pips directly (good) |
| **Act 2.4** | Double pass → second 2d6 is natural; max roll 3 has no target |
| **Map growth** | New hexes need tokens from classic pool |
| **Desert** | No token, no yield — clean |
| **Save/load** | Replace roll dicts with dice lists; v1 migration awkward |
| **Sim-runner** | Must simulate dice production; balance re-tune from scratch |

**Pros:** Pure Catan feel; tile placement matters most.  
**Cons:** Deletes Act 3 “max roll 3” knob; heavy card/event surgery; highest balance risk (production variance spikes on 6/8).

---

## Option (b) — Hybrid: tokens gate *which* hexes fire; resource rolls are weather multipliers

**Model:**

1. Each non-desert hex has a **number token** (classic multiset for up to 18 hexes).
2. Each night: roll **2d6** once per yield pass (Act 2+ = 2 passes, same as `RollNightlyCombined` pass count).
3. Keep existing **per-resource nightly rolls** (`TomorrowRolls` / `TodayRolls`) as a global weather/luck layer (0 = drought for that resource, 1 = normal, 2–3 = bumper).
4. **Production:** for each building-adjacent hex, if `TodayRolls[resource] > 0` **and** any `TodayDiceRolls` equals `tile.NumberToken`, add `TodayRolls[resource]` (settlement) or `ceil(×1.5)` (city), then apply existing modifiers (Sawmill, Gold Rush, CityProductionBoost, etc.).

| Area | Impact |
|------|--------|
| **Leaders** | RollInsurance / Monastery unchanged (still fix 0 resource rolls) |
| **Cards** | Drought / Fertile / Ledger / Forecast still modulate resource rolls; Forecast also rerolls dice preview |
| **Events** | Famine caps wheat **multiplier**; GoodHarvest +1 multipliers; Storm still blocks hex |
| **Shop / setup-bonus** | Unaffected (setup-bonus ignores tokens by design) |
| **AI** | Settlement score += token pip weight; robber targets high-pip human tiles |
| **Act 2.4** | **Preserved:** 2 roll passes → 2 dice sums; Act 3 max roll 3 caps resource multiplier |
| **Map growth** | `ExpandBoard` assigns tokens to new hexes from remaining pool; no duplicate illegal reds |
| **Desert** | `IsDesert` → no token, no yield |
| **Save/load** | Optional `numberToken` per tile + optional dice lists (v1 defaults: assign on load if missing) |
| **Sim-runner** | Same loop; production formula change only |

**Pros:** Keeps Fase 2.4 meaningful; minimal card/event churn; classic placement value without “number inflation” (multiplier cap still 3).  
**Cons:** Two layers to explain in UI (dice sum + resource multipliers).

---

## Option (c) — Optional variant / modifier

**Model:** Toggle in run setup; default remains abstract rolls only.

| Area | Impact |
|------|--------|
| **All systems** | Duplicate code paths or heavy `if (UseTokens)` branching |
| **Act 2.4 / balance** | Sim-runner must run both modes; tuning split |
| **Save/load** | Mode flag + conditional fields |
| **Player UX** | Most players never enable it — contradicts “real system” goal |

**Pros:** Safest rollback.  
**Cons:** Maintenance burden; tokens feel like debug flag; AI/cards need dual logic.

---

## Decision: **(b) Hybrid**

**Why:**

1. **Act 2.4 stays visible** — double yield pass maps to two nightly 2d6 sums; Act 3 max roll 3 still escalates the resource multiplier ceiling.
2. **Cards/events keep intent** — “Famine caps wheat rolls” and “Good Harvest +1” remain correct on the weather layer; Forecast rerolls both layers.
3. **No production explosion** — a hot 8 only pays when dice hit 8 *and* the resource multiplier is > 0; caps and robber/storm still apply.
4. **Classic placement** — AI and humans value 5/6/8/9 tokens via pip weights without retiring Monastery / RollInsurance.
5. **(a)** throws away too much shipped design; **(c)** hides the feature the milestone asks for.

---

## Card / event mapping (hybrid)

| Source | Old meaning | Hybrid meaning |
|--------|-------------|----------------|
| **Forecast** | Reroll all `TomorrowRolls` | Reroll resource rolls **and** `TomorrowDiceRolls` |
| **Famine** | Cap wheat roll at 1 | Same — wheat multiplier capped; dice can still hit wheat hexes for 1× |
| **GoodHarvest** | +1 all resource rolls (cap) | Same — bumps multipliers, not token values |
| **Drought / Fertile / Ledger** | Target one resource roll | Unchanged |
| **Monastery / RollInsurance** | Fix 0 resource roll | Unchanged |
| **Storm** | Block one hex | Unchanged — blocks even if dice match |
| **Gold Rush** | Double stone yield | Unchanged — applied after token+dice gate |
| **Bandit Raid (event)** | Robber → best human production tile | Score tiles by buildings × token pip weight |

---

## Token assignment rules

- Classic multiset for 18 producible hexes: one 2, one 12, two each of 3–6, 8–11 (no 7).
- Desert (`IsDesert`) and tiles without assignment: `NumberToken == null`.
- Placement greedy algorithm avoids adjacent **red** pairs (6 or 8 touching 6 or 8).
- `MapPresets.CreateBoard` assigns all tokens; `ExpandBoard` assigns only to new hexes from the unused pool.
- Large board center desert remains tokenless.

---

## Leftover risks

- **Balance:** hybrid variance should be validated with sim-runner on long Act 3 runs; hot-double 6/8 with Act 2 double dice may spike.
- **UI literacy:** players must see both dice sums and resource multipliers until a proper UI pass.
- **Legacy saves:** pre-2.5 saves without `numberToken` fields get tokens assigned on load from run seed (positions may differ from a fresh run).
