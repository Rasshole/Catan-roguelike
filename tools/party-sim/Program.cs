using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;

namespace CatanRoguelike.PartySim
{
    /// <summary>
    /// v142 balance study: plays many full party-rules matches AI-vs-AI (pure C# match core, no Unity) and writes CSVs.
    /// Usage: dotnet run -c Release -- [--games N] [--seats 3,4,5] [--seed S] [--out DIR] [--round-cap R] [--threads T]
    ///        [--timeout-ms MS] [--quest-pick ai|random]
    /// Not part of any test suite; run it by hand.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var o = Options.Parse(args);
            if (o.Trace != null)
                return GameRunner.Trace(o.Trace);
            Directory.CreateDirectory(o.OutDir);
            var jobs = new List<(int id, int seats, MapSize map, int seed)>();
            int id = 0;
            foreach (int n in o.Seats)
            {
                var maps = n >= 5 ? new[] { MapSize.Extended, MapSize.Huge } : new[] { MapSize.Large, MapSize.Extended };
                for (int g = 0; g < o.Games; g++)
                    jobs.Add((id++, n, maps[g % maps.Length], o.Seed + n * 100000 + g));
            }

            var results = new ConcurrentBag<GameRecord>();
            var sw = Stopwatch.StartNew();
            int done = 0;
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = o.Threads }, job =>
            {
                results.Add(GameRunner.Play(job.id, job.seats, job.map, job.seed, o.RoundCap, o.TimeoutMs, o.QuestPick == "random"));
                int d = System.Threading.Interlocked.Increment(ref done);
                if (d % 100 == 0)
                    Console.Error.WriteLine($"{d}/{jobs.Count} games, {sw.Elapsed.TotalSeconds:F0}s");
            });

            // v144: the timeout is wall-clock, so a box stall can cut in-flight games short. Games are deterministic per
            // seed, so replay each timed-out game alone (generous budget) and keep that result; only a game that still
            // times out on its own is a real stall.
            int retried = 0;
            foreach (var slow in results.Where(r => r.EndReason == "timeout").ToList())
            {
                retried++;
                var again = GameRunner.Play(slow.Id, slow.Seats, slow.Map, slow.Seed, o.RoundCap, o.TimeoutMs * 4, o.QuestPick == "random");
                Console.Error.WriteLine($"retry game {slow.Id} seed {slow.Seed}: {again.EndReason} in {again.Ms:F0} ms");
                results = new ConcurrentBag<GameRecord>(results.Where(r => r.Id != slow.Id).Append(again));
            }

            var list = results.OrderBy(r => r.Id).ToList();
            CsvWriter.WriteAll(o.OutDir, list);
            var bad = list.Where(r => r.EndReason != "win").ToList();
            Console.WriteLine($"{list.Count} games in {sw.Elapsed.TotalSeconds:F1}s; non-wins: {bad.Count}; timeouts retried alone: {retried}");
            foreach (var g in bad.Take(20))
                Console.WriteLine($"  game {g.Id} seed {g.Seed} {g.Seats}p {g.Map}: {g.EndReason} {g.Error}");
            return 0;
        }
    }

    internal sealed class Options
    {
        public int Games = 300;
        public int[] Seats = { 3, 4, 5 };
        public int Seed = 142000;
        public string OutDir = "balance-sim";
        public int RoundCap = 80;
        public int TimeoutMs = 30000;
        public int Threads = Environment.ProcessorCount;
        /// <summary>"ai" (shipped policy) or "random" (uniform among the offered quests, to measure 2-VP quests).</summary>
        public string QuestPick = "ai";
        /// <summary>"seed,seats,map": replay one game and print its log and the commands (debugging).</summary>
        public string Trace;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                string v = args[i + 1];
                switch (args[i])
                {
                    case "--games": o.Games = int.Parse(v); break;
                    case "--seats": o.Seats = v.Split(',').Select(int.Parse).ToArray(); break;
                    case "--seed": o.Seed = int.Parse(v); break;
                    case "--out": o.OutDir = v; break;
                    case "--round-cap": o.RoundCap = int.Parse(v); break;
                    case "--timeout-ms": o.TimeoutMs = int.Parse(v); break;
                    case "--threads": o.Threads = int.Parse(v); break;
                    case "--quest-pick": o.QuestPick = v; break;
                    case "--trace": o.Trace = v; break;
                    default: throw new ArgumentException("Unknown option " + args[i]);
                }
            }

            return o;
        }
    }

    internal sealed class SeatRecord
    {
        public int Seat;
        public string Civ, Trait, Quest, QuestOffer, TraitOffer, CivOffer;
        public int QuestVp, QuestDoneRound = -1;
        public bool QuestDone, Winner;
        public int Vp, Settlements, Cities, LongestRoad, LargestArmy, VpCards, QuestPoints, Knights, PlayerTrades, Rank;
        public int CardsAtEnd;
    }

    internal sealed class EventRecord
    {
        public int Round;
        public string Event;
        public int LeaderSeat, LeaderVp;
        public int HandsBefore, HandsAfter = -1;
        public string Detail = "";
    }

    internal sealed class ShenaniganRecord
    {
        public int Round, Seat = -1, Target = -1, Price = -1, Bidders = -1;
        public string Kind, Item = "", Detail = "";
    }

    internal sealed class GameRecord
    {
        public int Id, Seats, Seed, Rounds, Turns, Commands, Winner = -1, FirstSeat = -1;
        // v142: player trading + knights.
        public int Proposals, Accepts, Counters, Declines, Confirms, Cancels, Knights, KnightsPreRoll, LargestArmySeat = -1;
        public MapSize Map;
        public string EndReason = "win", Error = "";
        public double Ms;
        public List<SeatRecord> SeatRecords = new();
        public List<EventRecord> Events = new();
        public List<ShenaniganRecord> Shenanigans = new();
    }

    internal static class GameRunner
    {
        public static GameRecord Play(int id, int seats, MapSize map, int seed, int roundCap, int timeoutMs, bool randomQuests)
        {
            var g = new GameRecord { Id = id, Seats = seats, Map = map, Seed = seed };
            var sw = Stopwatch.StartNew();
            MatchController match = null;
            (string quest, string trait, string civ)[] offers = new (string, string, string)[seats];
            try
            {
                var config = MatchConfig.Create(seats, 0, seed, map, partyRules: true);
                match = new MatchController(config);
                var s = match.State;
                EventRecord openEvent = null;
                while (!match.IsOver)
                {
                    if (s.Round >= roundCap) { g.EndReason = "round_cap"; break; }
                    if (sw.ElapsedMilliseconds > timeoutMs) { g.EndReason = "timeout"; break; }
                    if (g.Commands > 300000) { g.EndReason = "command_cap"; break; }

                    var command = MatchRunner.PendingAiResponse(match) ?? MatchAi.ChooseCommand(match);
                    if (randomQuests && command != null && command.Type == MatchCommandType.PickQuest)
                    {
                        // Sim-only policy: pick uniformly among the offered quests (the shipped AI weighs points against its chances).
                        var offer = s.Seats[command.Seat].QuestOffer;
                        var pick = offer[(int)((uint)(seed * 31 + command.Seat * 7919) % (uint)offer.Count)];
                        command = MatchCommand.PickQuestTrait(command.Seat, pick, (MatchTraitId)command.Pick2);
                    }
                    if (command == null) { g.EndReason = "no_command"; g.Error = s.Phase.ToString(); break; }

                    var before = Snapshot.Take(s);
                    if (command.Type == MatchCommandType.PickQuest)
                    {
                        offers[command.Seat] = (string.Join("|", s.Seats[command.Seat].QuestOffer),
                            string.Join("|", s.Seats[command.Seat].TraitOffer), offers[command.Seat].civ);
                    }
                    else if (command.Type == MatchCommandType.PickCivilization)
                        offers[command.Seat] = (offers[command.Seat].quest, offers[command.Seat].trait,
                            string.Join("|", s.Seats[command.Seat].CivOffer));
                    string lastLine = s.Log.Count > 0 ? s.Log[^1] : null;
                    int round = s.Round;
                    var phaseBefore = s.Phase;
                    var result = match.Apply(command);
                    g.Commands++;
                    if (!result.Ok)
                    {
                        g.EndReason = "invalid_command";
                        g.Error = $"{command}: {result.Error}";
                        break;
                    }

                    switch (command.Type)
                    {
                        case MatchCommandType.ProposeTrade: g.Proposals++; break;
                        case MatchCommandType.RespondTrade:
                            if (command.Response == TradeResponseKind.Accepted) g.Accepts++;
                            else if (command.Response == TradeResponseKind.Countered) g.Counters++;
                            else g.Declines++;
                            break;
                        case MatchCommandType.ConfirmTrade: g.Confirms++; break;
                        case MatchCommandType.CancelTrade: g.Cancels++; break;
                        case MatchCommandType.PlayKnight:
                            g.Knights++;
                            if (phaseBefore == MatchPhase.Roll) g.KnightsPreRoll++;
                            break;
                    }

                    if (g.FirstSeat < 0 && s.Phase == MatchPhase.Roll)
                        g.FirstSeat = s.CurrentSeat;
                    var lines = NewLines(s.Log, lastLine);
                    Record(g, match, command, before, lines, round, ref openEvent);
                }

                if (openEvent != null && openEvent.HandsAfter < 0)
                    openEvent.HandsAfter = s.Seats.Sum(x => x.CardCount);
            }
            catch (Exception ex)
            {
                g.EndReason = "exception";
                g.Error = (ex.GetType().Name + ": " + ex.Message + " @ " + FirstFrame(ex)).Replace('\n', ' ').Replace(',', ';');
            }

            g.Ms = sw.Elapsed.TotalMilliseconds;
            if (match != null)
                Finish(g, match, offers);
            return g;
        }

        public static int Trace(string spec)
        {
            var parts = spec.Split(',');
            var config = MatchConfig.Create(int.Parse(parts[1]), 0, int.Parse(parts[0]), Enum.Parse<MapSize>(parts[2]), partyRules: true);
            var match = new MatchController(config);
            var s = match.State;
            int n = 0;
            while (!match.IsOver && n++ < 300000)
            {
                var command = MatchRunner.PendingAiResponse(match) ?? MatchAi.ChooseCommand(match);
                string last = s.Log.Count > 0 ? s.Log[^1] : null;
                var hands = string.Join(" ", s.Seats.Select(x => x.Hand.Total));
                var r = match.Apply(command);
                Console.WriteLine($"[r{s.Round} t{s.TurnNumber} {s.Phase} hands {hands}] {command} {(r.Ok ? "" : "REJECTED " + r.Error)}");
                foreach (var line in NewLines(s.Log, last))
                    Console.WriteLine("    " + line);
                if (!r.Ok) return 1;
            }

            return 0;
        }

        private static string FirstFrame(Exception ex)
        {
            var st = ex.StackTrace ?? "";
            var line = st.Split('\n').FirstOrDefault(l => l.Contains("CatanRoguelike")) ?? st.Split('\n').FirstOrDefault() ?? "";
            return line.Trim();
        }

        /// <summary>Lines appended by the last command (the log is capped, so find the previous last line by reference).</summary>
        private static List<string> NewLines(List<string> log, string lastLine)
        {
            int start = 0;
            if (lastLine != null)
            {
                start = -1;
                for (int i = log.Count - 1; i >= 0; i--)
                    if (ReferenceEquals(log[i], lastLine)) { start = i + 1; break; }
                if (start < 0) start = 0;
            }

            return log.Skip(start).ToList();
        }

        private sealed class Snapshot
        {
            public List<(int poster, int target, int total)> Bounties;
            public MatchAuctionResult Auction;
            public MatchBallotResult Ballot;
            public MatchPhase Phase;
            public int Hands;

            public static Snapshot Take(MatchState s) => new()
            {
                Bounties = s.Bounties.Select(b => (b.Poster, b.Target, b.Reward.Total)).ToList(),
                Auction = s.LastAuction,
                Ballot = s.LastBallot,
                Phase = s.Phase,
                Hands = s.Seats.Sum(x => x.CardCount)
            };
        }

        private static void Record(GameRecord g, MatchController match, MatchCommand c, Snapshot before, List<string> lines,
            int round, ref EventRecord openEvent)
        {
            var s = match.State;
            void Add(ShenaniganRecord r) { r.Round = s.Round; g.Shenanigans.Add(r); }

            foreach (var line in lines)
            {
                if (line.StartsWith("Event – ", StringComparison.Ordinal))
                {
                    if (openEvent != null && openEvent.HandsAfter < 0)
                        openEvent.HandsAfter = before.Hands;
                    var leader = s.Seats.OrderByDescending(x => x.VictoryPoints).ThenBy(x => x.Index).First();
                    openEvent = new EventRecord
                    {
                        Round = s.Round, Event = s.ActiveEvent.ToString(), LeaderSeat = leader.Index, LeaderVp = leader.VictoryPoints,
                        HandsBefore = before.Hands
                    };
                    g.Events.Add(openEvent);
                }
                else if (line.Contains(" claims ") && line.Contains("bounty"))
                    Add(new ShenaniganRecord { Kind = "bounty_claim", Seat = c.Seat, Detail = line.Replace(',', ';') });
                else if (line.StartsWith("Blight by", StringComparison.Ordinal))
                    Add(new ShenaniganRecord { Kind = "sabotage_bite", Item = "Blight", Seat = s.CurrentSeat });
                else if (line.StartsWith("Spoiled Grain by", StringComparison.Ordinal))
                    Add(new ShenaniganRecord { Kind = "sabotage_bite", Item = "SpoiledGrain", Seat = s.CurrentSeat, Detail = line.Contains("loses") ? "lost" : "nothing" });
            }

            // Bounties that left the board without a claim went back to their posters.
            int claims = lines.Count(l => l.Contains(" claims ") && l.Contains("bounty"));
            var after = s.Bounties.Select(b => (b.Poster, b.Target, b.Reward.Total)).ToList();
            var gone = before.Bounties.Where(b => !after.Contains(b)).ToList();
            foreach (var b in gone.Skip(claims))
                Add(new ShenaniganRecord { Kind = "bounty_return", Seat = b.poster, Target = b.target, Price = b.total });
            if (claims > 0)
            {
                // Attach target/reward to the claim rows just added.
                var claimRows = g.Shenanigans.Where(r => r.Kind == "bounty_claim" && r.Target < 0).ToList();
                for (int i = 0; i < claimRows.Count && i < gone.Count; i++)
                {
                    claimRows[i].Target = gone[i].target;
                    claimRows[i].Price = gone[i].total;
                    claimRows[i].Item = "poster" + gone[i].poster;
                }
            }

            switch (c.Type)
            {
                case MatchCommandType.CastVote:
                    Add(new ShenaniganRecord { Kind = "vote", Seat = c.Seat, Item = s.BallotDecree.ToString(), Detail = c.Pick == 1 ? "aye" : "nay" });
                    break;
                case MatchCommandType.PlaceBid:
                    Add(new ShenaniganRecord { Kind = "bid", Seat = c.Seat, Item = s.AuctionItem.ToString(), Price = c.Offer.Total });
                    break;
                case MatchCommandType.PostBounty:
                    Add(new ShenaniganRecord { Kind = "bounty_post", Seat = c.Seat, Target = c.Victim, Price = c.Offer.Total });
                    break;
                case MatchCommandType.BuySabotage:
                    Add(new ShenaniganRecord { Kind = "sabotage_buy", Seat = c.Seat });
                    break;
                case MatchCommandType.PlaySabotage:
                    Add(new ShenaniganRecord { Kind = "sabotage_play", Seat = c.Seat, Target = c.Victim, Item = ((MatchSabotageKind)c.Pick).ToString() });
                    break;
                case MatchCommandType.GuessSaboteur:
                    Add(new ShenaniganRecord
                    {
                        Kind = lines.Any(l => l.Contains(" unmasks ")) ? "guess_right" : "guess_wrong", Seat = c.Seat, Target = c.Victim
                    });
                    break;
                case MatchCommandType.RerollToken:
                    Add(new ShenaniganRecord { Kind = "reroll", Seat = c.Seat, Detail = lines.FirstOrDefault(l => l.Contains("rerolled"))?.Replace(',', ';') ?? "" });
                    break;
            }

            if (!ReferenceEquals(s.LastBallot, before.Ballot) && s.LastBallot != null)
            {
                var b = s.LastBallot;
                Add(new ShenaniganRecord
                {
                    Kind = "ballot", Item = b.Decree.ToString(), Price = b.Yes, Bidders = b.No, Seat = b.TieBreaker,
                    Detail = b.Passed ? "passed" : "failed"
                });
                if (openEvent != null) openEvent.Detail = $"{b.Decree}:{(b.Passed ? "passed" : "failed")}";
            }

            if (!ReferenceEquals(s.LastAuction, before.Auction) && s.LastAuction != null)
            {
                var a = s.LastAuction;
                Add(new ShenaniganRecord { Kind = "auction", Item = a.Item.ToString(), Seat = a.Winner, Price = a.Price, Bidders = a.Bidders });
                if (openEvent != null) openEvent.Detail = $"{a.Item}:{(a.Winner >= 0 ? "won" : "nobid")}:{a.Price}";
            }
        }

        private static void Finish(GameRecord g, MatchController match, (string quest, string trait, string civ)[] offers)
        {
            var s = match.State;
            g.Rounds = s.Round;
            g.Turns = s.TurnNumber;
            g.Winner = s.Winner ?? -1;
            g.LargestArmySeat = s.LargestArmySeat ?? -1;
            if (g.EndReason == "win" && g.Winner < 0)
                g.EndReason = "no_winner";
            var ranks = s.Seats.OrderByDescending(x => x.VictoryPoints).Select(x => x.Index).ToList();
            foreach (var seat in s.Seats)
            {
                var q = MatchPartyLibrary.Quest(seat.Quest);
                g.SeatRecords.Add(new SeatRecord
                {
                    Seat = seat.Index,
                    Civ = seat.Civilization.ToString(),
                    Trait = seat.Trait.ToString(),
                    Quest = seat.Quest.ToString(),
                    QuestOffer = offers[seat.Index].quest ?? "",
                    TraitOffer = offers[seat.Index].trait ?? "",
                    CivOffer = offers[seat.Index].civ ?? "",
                    QuestVp = q?.Points ?? 0,
                    QuestDone = seat.QuestDone,
                    Winner = s.Winner == seat.Index,
                    Vp = seat.VictoryPoints,
                    Settlements = s.Board.CountBuildings(seat.Owner, BuildingType.Settlement),
                    Cities = s.Board.CountBuildings(seat.Owner, BuildingType.City),
                    LongestRoad = s.LongestRoadSeat == seat.Index ? 2 : 0,
                    LargestArmy = s.LargestArmySeat == seat.Index ? 2 : 0,
                    VpCards = seat.DevCards.Count(d => d == MatchDevCard.VictoryPoint) + seat.NewDevCards.Count(d => d == MatchDevCard.VictoryPoint),
                    QuestPoints = seat.QuestVictoryPoints,
                    Knights = seat.KnightsPlayed,
                    PlayerTrades = seat.PlayerTrades,
                    Rank = ranks.IndexOf(seat.Index) + 1,
                    CardsAtEnd = seat.CardCount
                });
            }
        }
    }

    internal static class CsvWriter
    {
        public static void WriteAll(string dir, List<GameRecord> games)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("game,seed,seats,map,end_reason,winner,first_seat,rounds,turns,commands,ms,"
                                       + "proposals,accepts,counters,declines,confirms,cancels,knights,knights_preroll,largest_army_seat,events,error\n");
            foreach (var g in games)
                sb.Append(string.Join(",", g.Id, g.Seed, g.Seats, g.Map, g.EndReason, g.Winner, g.FirstSeat, g.Rounds, g.Turns,
                    g.Commands, g.Ms.ToString("F1", inv), g.Proposals, g.Accepts, g.Counters, g.Declines, g.Confirms, g.Cancels,
                    g.Knights, g.KnightsPreRoll, g.LargestArmySeat,
                    string.Join("|", g.Events.Select(e => e.Event)), g.Error)).Append('\n');
            File.WriteAllText(Path.Combine(dir, "games.csv"), sb.ToString());

            sb = new StringBuilder("game,seats,map,end_reason,seat,winner,civ,trait,quest,quest_vp,quest_done,vp,settlements,cities,"
                                   + "longest_road,largest_army,vp_cards,quest_points,knights,player_trades,rank,cards_at_end,quest_offer,trait_offer,civ_offer\n");
            foreach (var g in games)
            foreach (var r in g.SeatRecords)
                sb.Append(string.Join(",", g.Id, g.Seats, g.Map, g.EndReason, r.Seat, r.Winner ? 1 : 0, r.Civ, r.Trait, r.Quest, r.QuestVp,
                    r.QuestDone ? 1 : 0, r.Vp, r.Settlements, r.Cities, r.LongestRoad, r.LargestArmy, r.VpCards, r.QuestPoints,
                    r.Knights, r.PlayerTrades, r.Rank, r.CardsAtEnd, r.QuestOffer, r.TraitOffer, r.CivOffer)).Append('\n');
            File.WriteAllText(Path.Combine(dir, "seats.csv"), sb.ToString());

            sb = new StringBuilder("game,seats,winner,round,event,leader_seat,leader_vp,hands_before,hands_after,detail\n");
            foreach (var g in games)
            foreach (var e in g.Events)
                sb.Append(string.Join(",", g.Id, g.Seats, g.Winner, e.Round, e.Event, e.LeaderSeat, e.LeaderVp, e.HandsBefore, e.HandsAfter, e.Detail)).Append('\n');
            File.WriteAllText(Path.Combine(dir, "events.csv"), sb.ToString());

            sb = new StringBuilder("game,seats,winner,round,kind,seat,target,item,price,bidders,detail\n");
            foreach (var g in games)
            foreach (var r in g.Shenanigans)
                sb.Append(string.Join(",", g.Id, g.Seats, g.Winner, r.Round, r.Kind, r.Seat, r.Target, r.Item, r.Price, r.Bidders, r.Detail)).Append('\n');
            File.WriteAllText(Path.Combine(dir, "shenanigans.csv"), sb.ToString());
        }
    }
}
