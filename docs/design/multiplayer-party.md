# Multiplayer party mode: 4–5 friends online (design plan, v133)

> Status: **design only**. No netcode is implemented yet; this document scopes it.
> Audience: Rasmus' Catan group. The goal is a 60–90 minute online evening with 4–5 friends that feels like
> Catan plus a roguelike, with events, civilizations, and a lot of table talk and shenanigans.

## 1. Goals and non-goals

**Goals**

- 4–5 human players (AI bots fill empty seats), private lobbies joined with a short code, no accounts needed.
- Keep the single-player rules engine (`Assets/_Project/Scripts/Core`): one rules implementation, two front-ends.
- Turn-based, so latency barely matters. Correctness, reconnects and fairness matter far more.
- Lean into what single-player cannot do: **negotiation, votes, alliances, betrayal, auctions, sabotage**.
- A match is a self-contained **party run**: 3 acts (days and nights), civ and leader draft, events every night.

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
   - Seats 1–5, each a human, an empty slot, or **Bot (Easy/Normal/Hard)**. The host can kick, and can swap seats by drag.
   - Settings: map (Base 19 for 3–4 players, Extended 30 for 5–6), Fishermen on/off, **turn timer**, **shenanigan level**
     (Chill / Spicy / Chaos, which scales event and betrayal weights), and the victory target.
   - Ready-up. The host's **Start** is enabled when everyone is ready.
5. **Party run setup**, all simultaneous and with hidden picks:
   - The **leader pick** happens simultaneously, and duplicates are allowed.
   - The **civilization draft** is a **snake draft**: each player sees 3 offers, and picks are unique.
   - Then the **unique-building draft**. Picks are revealed together with a flourish.
6. **Reconnect**: if a client drops, its seat turns into "Away (bot plays safe)" for up to N minutes. Rejoining with the same code
   and player ID gets a snapshot and the seat back. **Host migration** is not in v1: if the host leaves, the match is
   saved and can be resumed by the same host later from `seed + log`.

## 5. Turn structure, timers and pacing

- **Day turns** follow the standard roll, trade, build order. **Special Build Phase** applies with 5+ players: after each turn,
  the other players may build in seat order, but not trade. This keeps 5-player games from dragging.
- **Turn timer**: the default is 90 s per turn, with a +30 s **bank** per player that refills each act. When it runs out, the turn auto-ends. Any
  open trade is cancelled. Any pending forced choice (discard, robber) is resolved by the bot policy.
- **Simultaneous phases** (nights, votes, auctions, drafts) use a shared 30–45 s timer. Everyone acts at once, and
  results are revealed together.
- **Nights** are where the roguelike part happens: an event card, shop deals, level-ups. These run in parallel for everyone,
  so a night costs about 30 s of wall time, not 5× that.
- A party run is **3 acts** of roughly 5 rounds each, with the VP target scaled to player count. The target is about 75 minutes.

## 6. Trading between humans

- **Offer board**: the active player posts an offer such as "give 2 Brick, want 1 Ore". Each other player sees **Accept / Counter / Decline**.
  The active player picks one accepted or countered offer, and the host executes it atomically after re-validating both hands.
- **Counter-offers** are posted on the same board, so everyone sees the haggling.
- **Promises are not enforced.** Chat-style "I'll give you wheat next turn" is just talk, and that is part of the fun.
  §7 adds **enforced** contracts for anyone who wants them.
- **Gifts** (giving resources away for free) are allowed but rate-limited, so one player can't kingmake by feeding another.
- Bank and port trades work as in single-player. The trade dialog grows a "Players" tab.
- **Quick chat** offers presets ("Anyone have Ore?", "Robber me and you're next"), plus emotes and free text.

## 7. Multiplayer shenanigans: events and mechanics

Each item is either a **night event** (drawn from the event deck, with weights scaled by the shenanigan level) or a **standing
mechanic**. All of them reuse a small set of host-side primitives: **Vote**, **SealedBid/Auction**, **Pact (escrow)**,
**SecretCard**, and **TargetedEffect**.

1. **Council Vote**: a decree is proposed, for example "All ports become 3:1 tomorrow" or "Robber is banned from deserts". Players vote
   simultaneously, and ties go to the player with the fewest VP. Leaders can buy extra votes with resources.
2. **Bounty Board**: anyone can post a resource bounty on a target, for example "break Anna's longest road" or "rob Mikkel".
   The bounty pays out automatically when the condition happens. The poster's identity is revealed.
3. **Alliances (Pacts)**: two players sign a 2-day pact with **escrowed collateral**. They get no robber against each other and a 1:1
   trade discount. Both partners must accept it.
4. **Betrayal**: breaking a pact early forfeits the collateral to the victim but grants a big one-off bonus, such as a free
   settlement or 2 resources of choice. Betrayals are announced with a fanfare.
5. **Sabotage cards** (secret): place a hidden "Blight" on a hex. It produces nothing for one player at the next roll, and it is
   revealed when it triggers. Victims may spend fish to find out who did it.
6. **Night Market Auction**: a rare card or unique building is auctioned. The format is sealed-bid in resources, with **second-price**
   rules so bluffing matters. The winner's bid goes to the bank, or to the poorest player in Spicy mode.
7. **Robber for Hire**: when a 7 is rolled, players may secretly bid resources. The highest bidder moves the robber instead of the roller.
8. **Tribute (rubber band)**: the VP leader must give one card of their choice to the last-place player, who chooses from two options
   offered by the leader.
9. **Secret Agendas**: each player gets a hidden objective at the run start, for example "own 3 ports", "never be robbed in act 2" or
   "betray someone". It is worth 2 VP when revealed at the end.
10. **Coup**: if one player holds Largest Army **and** Longest Road, the others may vote to strip one title, with a supermajority
    needed.
11. **Ransom**: a robber steal becomes an offer. The victim can pay 2 of another resource to keep the stolen card.
12. **Spy and Rumors**: spend fish to peek at another hand. A "Rumor" card lets you lie: the next peek at you shows a fake hand.
13. **Duel**: challenge another player to a dice-off for stakes both have agreed. Civs and leaders modify the roll (Warlord +1).
14. **Shared Calamity** (co-op beat): a plague or a fleet threatens everyone. The group must pool X resources by the end of the night or every
    player loses a building. It reveals who free-rides.
15. **Embargo Vote**: players vote to embargo one player's port trades for a day. The target may bribe voters.
16. **Toll Roads** (civ-flavoured standing rule): Sea Folk or Merchants can charge a 1-resource toll when another player's road
    extends from theirs, and the payer can refuse by spending a turn.
17. **Wager on the Dice**: before a roll, anyone may bet resources on the sum (2:1 for 6/8, 5:1 for 2/12).

Balance guardrails:

- No elimination.
- Every hostile effect has a counterplay (fish, votes, bribes) or a visible attribution.
- One shenanigan per night at most in Chill, two in Spicy, and unlimited in Chaos.

## 8. 5-player board and expansion rules

- **3–4 players**: Base map (19 hexes, 9 ports). This is the current default.
- **5 players**: **Extended** map: `MapSize.Extended` (30 land hexes, the "5–6 players" board) already exists. Extend it to
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
- **Vote, auction and pact modals** are built from the same painted card frames as the civ and leader cards.
- **Event log** gets per-player colours. Chat is wired to the network.

## 10. First implementation steps (in order, each one shippable and tested)

1. **Seat refactor (no networking):** replace the binary `PlayerId` with `SeatId` (0..N-1). Make `GameState` per-seat lists, and add
   a `ISeatController` (LocalHuman, Bot). Single-player stays green. Add 3- and 4-bot simulation tests.
2. **Command layer:** add `IGameCommand` records (serializable) and `GameController.Apply(SeatId, command)` → `Result`.
   Route every HUD action through it. Test: each HUD action maps to exactly one command.
3. **Deterministic RNG plus a state hash:** use PCG32 streams per subsystem and `GameState.ComputeHash()`. Test: replaying a recorded log gives an identical
   hash.
4. **Snapshot and redaction:** `GameStateView ForSeat(SeatId)` with JSON round-trip tests. Make sure hidden info never appears in other
   seats' views.
5. **Hot-seat mode (pass-and-play):** 2–5 local humans on one screen. This proves the N-player UI, trading and votes with no network.
6. **Transport interface:** `INetSession` (Create/Join(code)/Send/OnMessage/OnPeerLeft), with one implementation on Unity
   Multiplayer Services (Sessions API: Lobby + Relay, anonymous auth) and an in-memory loopback for tests.
7. **Lobby UI** (code, seats, bots, settings, ready) on the parchment kit, then host-start into the party-run setup.
8. **Turn timers, the away-bot and reconnect** (snapshot plus log tail).
9. **Human trading:** the offer board, counters and atomic execution.
10. **Shenanigan primitives** (Vote, SealedBid, Pact/escrow, SecretCard), then the first 5 events: Council Vote, Bounty Board,
    Night Market Auction, Robber for Hire and Shared Calamity.
11. **5-player rules:** the Extended map tokens and ports, Special Build Phase, and 2 new seat colours.
12. **Playtest night** with the group, then tune timers and shenanigan weights. Add Steam as a second transport if the group
    wants friend invites.

## 11. Open questions for the group

- Are bots allowed in a 5-seat game, or must everyone be human?
- Should betrayal be hidden or public? Should agendas be secret?
- Is a 75-minute target right, or do we want a "long campaign" mode across several evenings, saved from `seed + log`?
- Is a Steam release planned? That decides whether a Steam transport is worth building early.
