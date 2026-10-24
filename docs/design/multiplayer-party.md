# Multiplayer party mode: 4–5 friends online (design plan, v133; decisions recorded a728fcc and v134)

> Status: **design only**. No netcode is implemented yet; this document scopes it.
> Audience: Rasmus' Catan group. The goal is a 60–90 minute online evening with 4–5 friends that feels like
> Catan plus a roguelike, with events, civilizations, and a lot of table talk and shenanigans.

## 0. Decisions (Rasmus, 23 Oct 2026; second round v134)

| Topic | Status | Decision |
| --- | --- | --- |
| Secret quests | **DECIDED: YES** | Every player has a hidden quest, picked during setup (see §4.1), revealed and scored at the end. |
| Vote rounds | **DECIDED: YES, secret ballot** | Votes are cast hidden and simultaneously. Only the totals and the result are shown, never who voted for what. |
| Bounties (v1) | **DECIDED** | A bounty is claimed by **moving the robber onto one of the bounty-holder's tiles and stealing from them**. The claim is only valid if the target has at least one card. |
| Alliances and betrayal | **DECIDED: PARKED** | Moved to "Later" (§12). Not in v1. |
| Auctions | **DECIDED: YES, rare items only** | Only rare cards, or **the right to reroll the number token on one of your own tiles**. No auctions for plain resources or buildings. |
| Sabotage cards | **DECIDED: YES** | Secret sabotage cards are in v1. |
| Setup order | **DECIDED** | 1) Pick a **CIVILIZATION** (= its leader, see the next row) before placement, so you place with its ability in mind. 2) Place starting settlements and roads. 3) Pick a secret **QUEST** and a small **TRAIT**, offered based on your board position and what is still available. |
| Civilizations vs leaders | **DECIDED (v134): ONE concept** | A **Civilization is led by a Leader**; the leader's portrait is the civ's face. There is **one pick: choose 1 of 3 civs**, each card showing its leader portrait, the civ passive, the leader ability and any drawback. The separate leader step is removed. Each civ maps to a leader archetype (Merchant, Pioneer, Warlord, Architect) whose passive and level-up perk pool remain as the run-progression layer. Implemented in single-player in v134 (map → civ → unique draft → board). |
| Chaos rounds | **DECIDED** | Not a special mode. "Chaos" is just a normal event card in the night event deck. |
| Turn timer | **DECIDED** | **1.5 minutes per turn.** Each player starts with **1 "extra turn time" charge** and gains **+1 charge every 5 rounds**. |
| Extra-time charge | **DECIDED (v134)** | The player **activates it manually** (a button) and gets **+90 s**, at most once per turn. In the **last 5 seconds** of the turn timer the number **grows and pulses**, so you know it is time to use a charge. |
| Quest VP | **DECIDED (v134)** | A **mix of 1-VP (easier) and 2-VP (harder) quests**, so opponents cannot easily tell how many points you are missing. |
| Bots | **DECIDED (v134): bots = AI players** | Lobby and setup support **any mix of humans and AI**. Empty seats can be filled with AI, for example 3 humans + 1 AI for a 4-player game. |

Where an older section and this table disagree, this table wins. The sections below have been updated to match.

## 1. Goals and non-goals

**Goals**

- 4–5 human players (AI bots fill empty seats), private lobbies joined with a short code, no accounts needed.
- Keep the single-player rules engine (`Assets/_Project/Scripts/Core`): one rules implementation, two front-ends.
- Turn-based, so latency barely matters. Correctness, reconnects and fairness matter far more.
- Lean into what single-player cannot do: **negotiation, secret votes, bounties, secret quests, auctions, sabotage**
  (alliances and betrayal are parked, see §12).
- A match is a self-contained **party run**: 3 acts (days and nights), civilization (= leader) pick, secret quests, events every night.

**Non-goals for v1**

- Public matchmaking, ranked play, anti-cheat beyond "host is authoritative", or more than 6 seats.
- Real-time mechanics, and mobile cross-play (possible later; the UI is already canvas-scaled).

## 2. Where the code is today

| Fact | Consequence |
| --- | --- |
| `Core` is a plain C# assembly (`CatanRoguelike.Core.asmdef`). `GameController` owns `GameState`. | The sim can run headless on the host, with no scene. |
| RNG is seeded per run (`new Random(runSeed)` in `GameController`, `CardEngine`, `EventEngine`, `CivilizationOffer`, `RunProgression`). | A run is reproducible from **seed + ordered player inputs**, which gives replays, desync checks and reconnects. |
| `PlayerId` is `{ Human = 0, Ai = 1 }`, and `GameState` stores many "player vs AI" pairs. | **Biggest refactor:** seats must become `0..N-1` with a per-seat controller (local human, remote human, bot). |
| HUD calls `GameController` methods directly (for example `SelectLeader` and `ChooseCivilization`). | Introduce a **command layer** so the HUD sends intents instead of mutating state. |
| `com.unity.nuget.newtonsoft-json` is already a dependency. | Snapshots and commands can be JSON at first. Payloads are small, and binary can come later. |

## 3. Netcode recommendation

### 3.1 Options considered (Unity 6)

| Option | Pros | Cons | Verdict |
| --- | --- | --- | --- |
| **Unity Multiplayer Services (Sessions API: Lobby + Relay) + Unity Transport; NGO only for messaging** | First-party in Unity 6. Join codes are built in. Relay avoids NAT and port-forwarding issues. The free tier comfortably covers a friends' group. Works on any store. | Needs a UGS project and sign-in (anonymous auth is fine). NGO's GameObject replication is overkill for a board game. | **Recommended for v1.** |
| Steamworks (Steam lobbies + Steam Datagram Relay, via Facepunch.Steamworks or Steamworks.NET) | "Invite friend" and overlay feel native. Relay is free. No extra backend. | Steam-only. Needs an app ID (fee) and the Steam client running. | **Second transport later**, behind the same interface. |
| Photon Fusion / Realtime | Very quick to prototype. Hosted rooms. | CCU-based pricing. Fusion's tick-based state sync doesn't fit a turn-based sim. | Not needed. |
| Custom WebSocket server | Full control. Allows a dedicated server. | Hosting, ops and security become our problem. | Not for a friends' game. |

### 3.2 Model: host-authoritative, command in, view out

1. **Host** is one player's game, or later a headless build. It runs the only authoritative `GameController`.
2. **Clients** send **commands** (intents), for example `BuildRoad(edge)`, `ProposeTrade(offer)`, `EndTurn`, `CastVote(option)`.
3. The **host validates** each command with the same Core rules: placement, costs, phase and whose turn it is. It applies the command, appends it to the **command log**, and broadcasts it.
4. After every accepted command, the host sends each client a **redacted view snapshot** (other players' hands show only counts, dev cards stay hidden, and secret objectives and sabotage cards are hidden). Turn-based payloads are a few KB, so state sync is cheaper and simpler than lockstep.
5. **Determinism still pays off** because the sim is already seeded:
   - **Replays and bug reports:** `seed + command log` reproduces a match exactly.
   - **Desync and cheat detection:** the host sends `stateHash` with each command, and a debug client can re-simulate.
   - **Reconnect:** a full snapshot plus a log tail.
6. **Hidden information** never leaves the host unredacted, so a client cannot read other hands from memory.

Clients never run their own authoritative sim, so float or platform differences can't desync the game. A
"lockstep" mode, where every client simulates every command, stays possible later for spectators and replays,
but v1 does not depend on it.

### 3.3 Determinism hardening (needed for replays and hashes anyway)

- Replace `System.Random` in Core with a tiny seeded PCG32 or xoshiro, so streams are identical on Mono, IL2CPP and .NET.
- Derive one independent stream per subsystem from the run seed (dice, events, cards, AI, drafts), so an extra
  draw in one subsystem doesn't shift the others.
- Stop iterating unordered dictionaries in rules code.
- Add `GameState.ComputeHash()` and an EditMode test that replays a recorded command log and asserts the final hash.

## 4. Lobby and join flow

1. **Main menu → "Play with friends"**, with **Host** and **Join** options.
2. **Host** creates a session (Lobby + Relay allocation) and gets a **6-character join code**, for example `KT4-9QX`, plus a "copy code" button.
3. **Join**: enter the code, then pick a name and a colour (lobby metadata).
4. **Lobby screen**, built in the painted parchment style of the run-setup cards:
   - Seats 1–5 (minimum 2), each a human, an empty slot, or an **AI player** (DECIDED v134: any human/AI mix). A
     **"Fill empty seats with AI"** button turns every empty slot into an AI, so 3 friends can start a 4-player game.
     The host can kick, and can swap seats by drag.
   - Settings: map (Base 19 for 3–4 players, Extended 30 for 5–6), Fishermen on/off, turn timer (default 1.5 min plus
     charges, see §5), and the victory target. Chaos is a normal event, so there is no shenanigan-level setting.
   - Ready-up. The host's **Start** is enabled when everyone is ready.
5. **Party run setup** follows the decided order in §4.1.
6. **Reconnect**: if a client drops, its seat turns into "Away (bot plays safe)" for up to N minutes. Rejoining with the same code
   and player ID gets a snapshot and the seat back. **Host migration** is not in v1: if the host leaves, the match is
   saved and can be resumed by the same host later from `seed + log`.

### 4.1 Setup order (DECIDED)

1. **Civilization pick (= leader), before placement (DECIDED v134).** Civs and leaders are one concept: each player is
   offered **3 civilizations**, each led by a named leader whose portrait is the civ's face, and keeps one. Picks are hidden until
   everyone has chosen, then revealed together. Offers are dealt from one shared seeded pool, so no two players get the same civ. You then
   know your civ passive and your leader ability (for example the Merchant Republic's 3:1 trades plus the Merchant's
   cheaper shop) **before** you place. AI seats pick with the same rules.
2. **Starting placement.** Snake order (1→N, then N→1) for settlements and roads, with the starting player chosen by
   the seeded roll. This uses the normal timer.
3. **Secret quest and trait.** After placement, each player is offered **3 quests** and **2 small traits** and keeps one of each.
   - **Offers depend on board position.** Quests are filtered by what your start makes possible, for example "own 2
     ports" only if you can reach a port within 3 roads, or "control 3 Ore hexes" only next to ore. Each offer
     includes one "stretch" quest.
   - **Availability.** Quests and traits are unique per match, and the offers are dealt from one shared seeded pool
     in reverse placement order. The last player to place picks first, as compensation.
   - **Traits are small**, for example "+1 card on a roll of 2 or 12", "Robber steals from you show a decoy card",
     "First port trade each day is 1 better" or "Start with 1 fish".
   - **Quest VP (DECIDED v134): a mix of 1-VP (easier) and 2-VP (harder) quests.** Every offer contains both kinds, so
     nobody can tell from the outside how many points a hidden quest is worth.
   - The quest stays hidden until it completes (it is revealed then and scores) or until the end of the run.
4. **Unique-building draft.** The civilization question is settled by step 1. The unique-building draft is still open for
   multiplayer: proposal is to skip it in v1 to keep setup short. In single-player it stays as step 3 (map → civ → draft).

## 5. Turn structure, timers and pacing

- **Day turns** follow the standard roll, trade, build order. **Special Build Phase** applies with 5+ players: after each turn,
  the other players may build in seat order, but not trade. This keeps 5-player games from dragging.
- **Turn timer (DECIDED): 1.5 minutes per turn.**
  - Each player starts with **1 extra-turn-time charge** and gains **+1 charge every 5 rounds**. Unused charges carry over.
  - **DECIDED (v134):** the player **activates a charge manually** (a button next to the timer) at any point during
    their own turn and gets **+90 s**. At most one charge per turn.
  - **Last 5 seconds:** the timer number grows and pulses (bigger as time runs out), so you notice in time to use a charge.
  - When the timer runs out, the turn auto-ends. Any open trade offer is cancelled. Pending forced choices (discard,
    robber, steal target) are resolved by the bot policy.
  - The HUD shows a ring timer on the active player's row plus the number of charges (hourglass pips) for every player.
- **Simultaneous phases** (nights, secret ballots, auctions, the civilization pick, quest picks) use a shared 30–45 s timer. Everyone acts at once, and
  results are revealed together.
- **Nights** are where the roguelike part happens: an event card, shop deals, level-ups. These run in parallel for everyone,
  so a night costs about 30 s of wall time, not 5× that.
- A party run is **3 acts** of roughly 5 rounds each, with the VP target scaled to player count. The target is about 75 minutes.

## 6. Trading between humans

- **Offer board**: the active player posts an offer such as "give 2 Brick, want 1 Ore". Each other player sees **Accept / Counter / Decline**.
  The active player picks one accepted or countered offer, and the host executes it atomically after re-validating both hands.
- **Counter-offers** are posted on the same board, so everyone sees the haggling.
- **Promises are not enforced.** Chat-style "I'll give you wheat next turn" is just talk, and that is part of the fun.
  Enforced contracts (pacts) are parked, see §12.
- **Gifts** (giving resources away for free) are allowed but rate-limited, so one player can't kingmake by feeding another.
- Bank and port trades work as in single-player. The trade dialog grows a "Players" tab.
- **Quick chat** offers presets ("Anyone have Ore?", "Robber me and you're next"), plus emotes and free text.

## 7. Multiplayer shenanigans: events and mechanics

Each item is either a **night event** (drawn from the event deck) or a **standing mechanic**. All of them reuse a small set of
host-side primitives: **SecretBallot**, **SealedBidAuction**, **Bounty**, **SecretCard** and **TargetedEffect**.
Status tags: **v1** means it is in the first multiplayer release, **later** means parked.

### 7.1 Decided for v1

1. **Secret quests (DECIDED, v1):** each player has a hidden quest from setup step 3 (§4.1). A quest is worth **1 VP
   (easier) or 2 VP (harder)** (DECIDED v134, so the value can't be read from the outside) and is revealed when it completes, for example "own 3 ports", "never be robbed during act 2" or "claim a bounty". Quests can
   win the game, so table talk about who is going for what becomes part of the fun.
2. **Secret-ballot vote rounds (DECIDED, v1):** a night event proposes a decree, for example "all ports are 3:1 tomorrow",
   "the robber can't enter deserts" or "the richest player discards to 7". Everyone votes **hidden and at the same time**.
   Only the totals are shown. Ties go to the player with the fewest VP.
3. **Bounties (DECIDED, v1 rule):** any player can post a resource bounty on another player. It is paid into escrow and shown
   on the bounty-holder's panel. **The bounty is claimed by moving the robber (a 7 or a Knight) onto one of the bounty-holder's tiles
   and stealing from them.** The claim is valid **only if the bounty-holder has at least one card**. Otherwise the robber
   still moves but the bounty stays up. Unclaimed bounties return to the poster at the end of the act.
4. **Rare auctions (DECIDED, v1):** the Night Market sometimes auctions **one rare item**:
   - a rare card (for example a legendary development card or a unique building), or
   - **the right to reroll the number token on one of your own tiles**. The new token is drawn from the remaining pool,
     and 6/8 can't be placed next to each other, as in normal setup.

   The format is sealed-bid and simultaneous, in resources, at **second price** (the winner pays the second-highest
   bid). The payment goes to the bank. Plain resources and buildings are never auctioned.
5. **Sabotage cards (DECIDED, v1):** secret cards you can buy or win at night and play face-down:
   - **Blight**: a hex produces nothing for one chosen player on the next roll.
   - **Washed-out Road**: one road segment can't count toward Longest Road for a day.
   - **Spoiled Grain**: the target discards 1 Wheat at dawn.

   A sabotage card is revealed (with its author) when it triggers. The victim may spend fish to cancel it if they guess the author correctly.
6. **Chaos (DECIDED: just an event):** "Chaos" is one ordinary card in the night event deck, with normal weight, for example "all
   robbers move randomly and everyone swaps one random card to the left". There is no separate chaos mode or shenanigan-level setting.

### 7.2 Other v1 candidates (not decided yet, simple and compatible with the decisions)

7. **Robber for Hire**: on a 7, players may secretly bid to move the robber instead of the roller. This makes bounty claims more fun.
8. **Tribute**: the VP leader gives one card to the last-place player.
9. **Coup**: a secret ballot to strip one title (Largest Army or Longest Road) from a player who holds both. Needs a supermajority.
10. **Ransom**: a stolen card can be bought back for 2 resources of another kind.
11. **Spy and Rumors**: spend fish to peek at a hand. A Rumor card shows a fake hand to the next peek.
12. **Shared Calamity**: the group pools X resources by the end of the night, or every player loses a building.
13. **Embargo**: a secret-ballot vote blocks one player's port trades for a day.
14. **Wager on the Dice**: before a roll, anyone may bet on the sum.

### 7.3 Parked for later (DECIDED)

- **Alliances (pacts with escrowed collateral)** and **Betrayal** (breaking a pact for a one-off bonus) are parked. See §12.
- **Duels** and **Toll Roads** are also deferred until the v1 set has been playtested.

Balance guardrails: no elimination. Every hostile effect has a counterplay (fish, votes) or visible attribution when it triggers.
There is at most one hostile night event per night.

## 8. 5-player board and expansion rules

- **3–4 players**: Base map (19 hexes, 9 ports). This is the current default.
- **5 players**: **Extended** map: `MapSize.Extended` (30 land hexes, the "5–6 players" board) already exists. In v135
  it became the symmetric 3-4-5-6-5-4-3 island. It mirrors left/right and top/bottom about its own centre, and the
  camera frames it centred. Extend it to
  **11 ports** and a 28-token number set, following the 5–6 expansion distribution. Also add more bank cards: 24 per resource instead of 19.
- **Special Build Phase** is on for 5+ players (see §5). The **discard limit stays at 7**.
- Pieces per player stay the same (15 roads, 5 settlements, 4 cities).
- **Seat colours**: red, blue, white, orange, green, plus brown for 6. The painted piece materials need 2 more tints.
- **Setup order**: snake (1→5, 5→1), with the starting player chosen by the seeded roll.
- **Fishermen**: more fish tokens on the Extended coast. Fish shop prices stay the same.
- **Victory target**: 10 VP for 3–4 players, and 11 VP for 5 with Extended (tunable in the lobby).

## 9. UI changes (parchment kit)

- A **player strip** for 4–5 players: the rail's player panels become compact rows with the leader medallion, civ emblem, VP,
  card count and a turn-timer ring. The v133 leader portraits are meant to be reused here.
- **Trade dialog**: a "Players" tab with an offer board, plus incoming-offer toasts.
- **Quest card** (hidden, peekable) and **trait chip** on your own row. Bounty badges on targeted players.
- **Secret-ballot, auction and sabotage modals** are built from the same painted card frames as the civ and leader cards.
- **Event log** gets per-player colours. Chat is wired to the network.

## 10. Phased implementation plan (DECIDED: single device first, then netcode)

Every phase ends green in EditMode and PlayMode, with screenshots, and is playable on its own. Netcode is deliberately
last. Everything before it is useful immediately as **local hot-seat** (pass-and-play) on one PC, which is also
how the group can playtest the rules before any server exists.

### Phase 1: N-player core (no UI change for single-player) — **DONE in v134**
- Implemented as a separate match core, `Core/Match` (`MatchConfig`, `MatchController`, `MatchCommand`, `MatchAi`,
  `TurnClock`), so the single-player roguelike `GameController` is untouched. Seats 0..N-1 (N = 2..5) map 1:1 to board
  owners, so `BoardState`, `PlacementValidator` and `RouteCalculator` are reused as-is.
- `MatchConfig`: 2–5 seats, **any human/AI mix** (`SeatKind.Human`/`Ai`), `FillEmptySeatsWithAi()`, Base 19 board up to 4
  seats and Extended 30 for 5.
- **Command layer:** every action is a serializable `MatchCommand` (JSON) applied with `MatchController.Apply(command)` →
  `MatchCommandResult`. Wrong-seat or illegal commands are rejected and not logged. Accepted commands form a log;
  `MatchController.Replay(config, log)` rebuilds the same state (`MatchState.ComputeHash()`).
- Rules in the core: snake setup (the 2nd settlement pays out), roll, robber + steal, automatic discard on 7, roads, settlements,
  cities, 4:1 bank trades, longest road (2 VP), the win at 10 VP, and the extra-time charge (`UseExtraTime`, +1 per 5 rounds).
  Not yet: ports, development cards, player-to-player trades, Special Build Phase.
- `MatchAi` is the deterministic AI player, used for AI seats and as the timeout policy for humans.
- Tests: `MatchCoreV134Tests`. Seeded all-AI games for 2–5 seats finish, the same seed gives the same log and hash, and a JSON log replays to the same hash.
- Still to do from the original Phase 1 text: the single-player `GameController` itself is **not** command-based yet.
  That is deferred to Phase 5, where it matters for netcode.

### Phase 2: Local hot-seat for 4–5 players and the turn timer
- A 4–5 seat player strip on the rail (civ/leader medallion, VP, card count, timer ring, extra-time pips).
- **Hand-off screen** between human turns ("Pass to Anna"), which hides the previous hand.
- **Turn timer: 1.5 min, 1 starting extra-time charge, +1 every 5 rounds**, a manual **"+90 s" charge button**, the
  number growing and pulsing in the last 5 s, auto-end, and AI resolution of pending choices.
- 5-player rules: Extended board tokens and ports, Special Build Phase, 2 extra seat colours, 24 cards per resource.
- **Exit:** a full 4-human hot-seat game on Base and a 5-human game on Extended, with the timer and charges working.
- **Status (v134): started.** `Core/Match/HotSeatSession` drives the timer, the charges, the pass screen and the AI seats,
  and `RunHudMatchHotSeat` renders the table. You reach it from **Pass & play (2–5)** on the map screen. The lobby has a seat count
  stepper, Human / AI / Open per seat and **Fill open seats with AI**. The table shows a seat strip, the timer with a
  +90 s charge button that pulses in the last 5 s, a hand panel that is hidden on others' turns, and a pass screen.
  v135 filled the rule gaps:
  - **Board clicks** (`MatchBoardPick` + `HotSeatBoardInput`). Every spot the acting human can use glows: setup
    corners and edges, affordable settlements, cities and roads, free roads, and robber tiles. A click becomes the
    matching command. The suggestion buttons stay as shortcuts.
  - **Real harbours.** These are the same ports as the 3D board (`PortAccess.DiscoverPorts`). A seat trades 2:1 at
    its resource's harbour, 3:1 at a generic one, and 4:1 otherwise. The trade board shows each seat's rates.
  - **Development cards.** The deck has 25 cards (14 Knights, 5 VP, 2 Road Building, 2 Year of Plenty, 2 Monopoly).
    For 5+ seats it has 34 (20 / 5 / 3 / 3 / 3). A card costs 1 grain, 1 wool and 1 ore. You can play one card per
    turn, and not one bought this turn. Knights can be played before the roll. **Largest Army** is 3+ knights for
    2 VP. VP cards count at once but stay hidden: the seat strip shows public VP only.
  - **Trade board.** The current player offers give/get terms. Every other seat answers Accept, Decline or
    **Counter**; AI seats answer at once. The offerer then confirms one partner, on the original or the countered
    terms. Building waits while an offer is open, and the offer lapses at end of turn.
  - **Manual discard on a 7.** Each seat over 7 cards picks exactly half. In pass & play, every human discarder gets
    their **own pass screen**, so hands stay hidden. AI seats discard automatically, and a timeout discards for that
    seat only.

  Still to do: Special Build Phase, 24 bank cards per resource, and the 28-token / 11-port Extended set (§8).

### Phase 3: Decided setup flow
- **Civilization (= leader) first** (hidden simultaneous picks via hand-off, 3 offered each), then snake placement, then
  the **quest and trait offer**.
- Quest engine: data-driven `QuestDefinition` (condition, **1 or 2 VP**, board-feasibility filter) with a seeded unique
  pool. Every offer mixes 1-VP and 2-VP quests. Traits work the same way.
- **Exit:** offers change with board position (tested), and quests reveal and score correctly.
- **DONE in v136 (pass & play, "Party rules" toggle in the lobby, on by default):**
  - `MatchPhase.CivPick` before placement: each seat sees 3 civilizations (leader portrait, passive, drawback) behind
    its own pass screen; the offers come from one seeded deck, so no civ repeats in a match. Civs become public on the
    seat strip once everyone has picked.
  - Snake placement, then `MatchPhase.QuestPick` in **reverse seat order** (the last placer picks first): 3 quests
    (always both 1-VP "easier" and 2-VP "harder"), filtered by the seat's starting spots, unique per match,
    plus 2 small traits. Private on the pass screen; the seat's own panel shows civ, trait and quest progress.
  - Quest VP is never on the seat strip (`PublicVictoryPoints`); the win check counts it, and the game-over card
    reveals every quest (done ✓ / missed ✗) and trait.
  - The match event deck draws on the match cadence (from round 2); the active event shows on a card top-left.
  - Robber **victim picker**: when the chosen tile touches two or more robbable opponents, a picker docked over the
    left column asks whom to rob (AI seats rob the leader by public points, so secret quest VP never steers them).
  - Hot-seat table framing: dedicated play-area insets (board clear of the timer ring), robber capped at 1.35× and
    pushed off the number token. Pieces already use the single-player rule (settlement 0.18 of the hex width, city
    1.5×); a PlayMode test asserts it.
- **v137 fixes:**
  - Terrain is shuffled per seed for every match (19-hex and 30-hex), like dealing the tiles at a real table: no
    same-terrain group larger than 2 and only a few same-terrain edges, desert in the centre, no adjacent 6/8. The
    30/37-hex islands used to fill the ratio multiset in coordinate order (all ore on one side); they are now shuffled
    for single-player too (map pick and act expansion), while the designed single-player 7/13/19-hex maps are unchanged.
  - The seat strip shows each seat's own leader in a seat-coloured ring with the civ name (seat number / AI tag on a
    badge); before the civs are revealed every seat shows a neutral silhouette.
- **v138 (civ popup + icon trading):**
  - No always-visible civ/quest panel. Clicking a leader portrait in the seat strip (or your civ emblem on the
    single-player rail) opens a painted parchment popup beside the list: civ, leader, passive and drawback. The
    trait and the secret quest (with its VP and progress) only show on your own seat's popup (the seat holding the
    device); other seats' popups say they stay hidden until the game ends. It closes on a second click on the same
    portrait, a click outside, the ✕, or when the device is passed.
  - Trading uses icons and numbers instead of text (colonist.io-style, in our painted look): a red arrow on the
    "you give" row and a green arrow on the "you get" row, click an icon to add and right-click (or −) to remove,
    rate badges per resource (4:1 / 3:1 / 2:1, harbours in gold), compact per-player answers with ✓ / ✗ / ⇄ and
    icon counters. Incoming offers are always drawn from the viewer's side (what you give is red). On a shared
    device the offerer keeps the board; clicking a responder's disc hands the view to that seat to answer.
- **v139 (leaders on the trade card + wildcard trades):**
  - Every trade surface (hot-seat composer, offer header, answer rows, the clickable hand-off discs, the
    single-player answer row and incoming offer) shows the seat's leader portrait in a seat-coloured ring, like
    the seat strip. Without party rules (no civ yet) it falls back to the coloured disc with the seat number /
    "AI". The single-player AI has no civ and shows the neutral leader silhouette with an "AI" badge.
  - Rows are swapped everywhere: "you get" (green ↓) on top, "you give" (red ↑) below — also for incoming offers
    and counters, always from the viewer's side. The hot-seat card sits left of the board so it no longer covers it.
  - A painted wildcard card (all 5 resources fanned on one parchment card) can be added to one side of an offer.
    Give row: "N of any resource you choose" — the accepter picks when accepting. Get row: "N of anything" — the
    offerer picks from what the accepter can spare when confirming. The pick uses an icon picker and is limited to
    what the giver holds, never counting resources already named in the same trade. An offer may carry wildcards
    only if the offerer holds N spare cards outside the requested resources; counters carry none.
  - Rules: `MatchCommand.Propose(seat, give, get, giveAny, getAny)`, `AcceptWithChoice(seat, choice)` and
    `Confirm(seat, partner, choice)` are plain serializable commands (JSON round-trip, replay hash includes the
    wildcards and picks), validated in `MatchController` via `TradeWildcards`. The AI accepts a wildcard offer when
    the card count is fair and takes the resource it holds fewest of; single-player uses the same helpers.

### Phase 4: Shenanigans on one device
- Primitives: `SecretBallot`, `SealedBidAuction`, `Bounty` (robber-claim rule), `SecretCard` (sabotage).
- Content: secret-ballot decrees, bounties, rare auctions (rare card / reroll one of your own tokens), the 3 sabotage cards, and the Chaos event.
- Hidden inputs on hot-seat use the hand-off screen, one voter or bidder at a time.
- **Exit:** a playtest evening on one PC with the Catan group. Tune weights, prices and quest VP.

### Phase 5: Network-ready core
- PCG32 RNG streams per subsystem, `GameState.ComputeHash()`, and a recorded log that replays to the same hash.
- `GameStateView ForSeat(SeatId)` redaction (hands, quests, traits, sabotage, sealed bids) plus JSON snapshots.
- An in-memory loopback "network" in tests: 5 virtual clients play through commands and views only.
- **Exit:** a loopback 5-seat game is identical to hot-seat, and no hidden info appears in any other seat's view.

### Phase 6: Netcode (online with friends)
- `INetSession` implemented on Unity Multiplayer Services (Lobby + Relay, anonymous sign-in, join code).
- Lobby UI (seats, bots, settings, ready). The host-authoritative loop sends commands in and views out.
- Reconnect (snapshot plus log tail) and an away-bot. Saved matches can be resumed by the host.
- **Exit:** an online 5-player evening runs through without desyncs or lost turns.

### Phase 7: Later
- Alliances and betrayal (parked), duels, toll roads, Steam transport and friend invites, spectators and replays.

## 11. Open questions for the group

Resolved in v134 (see §0):
- ~~Where do civilizations go in multiplayer setup?~~ **DECIDED:** civs and leaders are merged, and the civ (= leader) pick comes before placement.
- ~~How much time does an extra-time charge add?~~ **DECIDED:** +90 s. It is activated manually, and the timer pulses in the last 5 s.
- ~~Quest VP value?~~ **DECIDED:** a mix of 1-VP (easier) and 2-VP (harder) quests.
- ~~Are bots allowed?~~ **DECIDED:** bots = AI players. Any human/AI mix is allowed, and empty seats can be filled with AI.

Still open:
- Unique-building draft in multiplayer: skip in v1 (proposed) or keep after the civ pick?
- Should a player get one quest or two (keep one, discard one)?
- ~~Dedicated portraits for the 6 emblem-only civs~~ **DONE (v135).** All 10 civs have a leader portrait, and each
  civ's leader has its own passive (`LeaderTraits`). For example, the Sea Folk's Tidecaller gives 1 fish each
  morning, and the Sun Kingdom's Sun Tithe gives 1 grain on a 7. The four archetype passives (Merchant, Pioneer,
  Warlord, Architect) apply only to their own civ.

## 12. Later (parked)

- **Alliances (DECIDED: parked):** a 2-day pact with escrowed collateral: no robber against each other, and 1:1 trades between the pair.
- **Betrayal (DECIDED: parked):** breaking a pact early forfeits the collateral but grants a one-off bonus, announced publicly.
- These build on the v1 primitives (escrow from bounties, attribution from sabotage), so they are cheap to add once v1 has been playtested.
