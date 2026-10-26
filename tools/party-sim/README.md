# party-sim (v142 balance study)

Headless AI-vs-AI self-play of full **party-rules** matches on the pure C# match core
(`Assets/_Project/Scripts/Core`). No Unity, no captures, not part of any test suite.

```bash
cd tools/party-sim
dotnet run -c Release -- --games 1000 --out /workspace/balance-sim                 # 1000 games per seat count (3, 4, 5)
dotnet run -c Release -- --games 1000 --quest-pick random --seed 242000 --out /workspace/balance-sim/random-quests
dotnet run -c Release -- --trace 642605,5,Huge                                      # replay one game with its log
cd /workspace/balance-sim && python3 /path/to/tools/party-sim/analyse.py            # tables (needs pandas)
```

- Boards: 3–4 players alternate Large / Extended, 5 players Extended / Huge.
- Stops a game at a win, `--round-cap` (80), 300k commands or `--timeout-ms` (30 s); exceptions and rejected
  AI commands are recorded as `end_reason`, never thrown.
- `--quest-pick random` swaps only the quest pick for a uniform pick among the offered quests (the shipped AI
  always takes a 1-VP quest), so 2-VP quests get data too.
- Output: `games.csv` (one row per game), `seats.csv` (civ, trait, quest and offers, VP breakdown, rank),
  `events.csv` (each event card with the VP leader at that moment and the hand totals before/after the round),
  `shenanigans.csv` (votes, ballots, bids, auctions, bounties posted/claimed/returned, sabotage bought/played/
  bitten, saboteur guesses, rerolls).
- ~3000 games take about 4 minutes on 8 cores.
