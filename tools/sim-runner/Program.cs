using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Victory;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.SimRunner
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var opts = Options.Parse(args);
            var results = new List<RunResult>(opts.Runs);

            for (int i = 0; i < opts.Runs; i++)
            {
                int seed = opts.SeedStart + i;
                var box = new RunBox();
                var task = Task.Run(() => MatchDriver.Play(seed, opts, box));
                if (!task.Wait(opts.TimeoutMs + 250))
                {
                    results.Add(MakeTimeout(seed, box, opts));
                    continue;
                }

                if (task.IsFaulted)
                {
                    var ex = task.Exception?.GetBaseException();
                    results.Add(new RunResult
                    {
                        seed = seed,
                        status = "crash",
                        days = box.Days,
                        steps = box.Steps,
                        winner = "none",
                        playerVp = box.PlayerVp,
                        aiVp = box.AiVp,
                        phase = string.IsNullOrEmpty(box.Phase) ? "unknown" : box.Phase,
                        ms = box.ElapsedMs,
                        error = ex == null ? "unknown" : ex.GetType().Name + ": " + ex.Message
                    });
                    continue;
                }

                results.Add(task.Result);
            }

            PrintReport(results, opts);
            return 0;
        }

        private static RunResult MakeTimeout(int seed, RunBox box, Options opts)
        {
            return new RunResult
            {
                seed = seed,
                status = "timeout",
                days = box.Days,
                steps = box.Steps,
                winner = "none",
                playerVp = box.PlayerVp,
                aiVp = box.AiVp,
                phase = string.IsNullOrEmpty(box.Phase) ? "unknown" : box.Phase,
                ms = opts.TimeoutMs,
                error = "wall-clock timeout"
            };
        }

        private static void PrintReport(List<RunResult> results, Options opts)
        {
            int ok = 0, timeout = 0, crash = 0, capped = 0;
            int human = 0, ai = 0, none = 0;
            long daysSum = 0;
            int daysN = 0;

            foreach (var r in results)
            {
                switch (r.status)
                {
                    case "ok": ok++; break;
                    case "timeout": timeout++; break;
                    case "crash": crash++; break;
                    default: capped++; break;
                }

                switch (r.winner)
                {
                    case "human": human++; break;
                    case "ai": ai++; break;
                    default: none++; break;
                }

                if (r.status != "timeout" && r.status != "crash")
                {
                    daysSum += r.days;
                    daysN++;
                }
            }

            var summary = new Summary
            {
                runs = results.Count,
                driver = "narrow-core",
                timeoutMs = opts.TimeoutMs,
                maxSteps = opts.MaxSteps,
                maxDays = opts.MaxDays,
                ok = ok,
                timeout = timeout,
                crash = crash,
                capped = capped,
                winsHuman = human,
                winsAi = ai,
                unfinished = none,
                avgDays = daysN == 0 ? 0 : Math.Round(daysSum / (double)daysN, 2),
                results = results
            };

            Console.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = false }));
            Console.WriteLine("seed status     days winner pVP aVP phase                    ms");
            foreach (var r in results)
            {
                string phase = r.phase ?? "";
                if (phase.Length > 23) phase = phase.Substring(0, 23);
                Console.WriteLine(
                    $"{r.seed,-4} {r.status,-10} {r.days,4} {r.winner,-6} {r.playerVp,3} {r.aiVp,3} {phase,-23} {r.ms,5}");
            }
        }
    }

    internal sealed class Options
    {
        public int Runs = 1;
        public int SeedStart = 1;
        public int TimeoutMs = 5000;
        public int MaxSteps = 300;
        public int MaxDays = 12;
        public MapSize MapSize = MapSize.Small;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : "";
                switch (a)
                {
                    case "--runs": o.Runs = Math.Max(1, int.Parse(Next())); break;
                    case "--seed-start": o.SeedStart = int.Parse(Next()); break;
                    case "--timeout-ms": o.TimeoutMs = Math.Max(200, int.Parse(Next())); break;
                    case "--max-steps": o.MaxSteps = Math.Max(1, int.Parse(Next())); break;
                    case "--max-days": o.MaxDays = Math.Max(1, int.Parse(Next())); break;
                    case "--map":
                        o.MapSize = Next().ToLowerInvariant() switch
                        {
                            "medium" => MapSize.Medium,
                            "large" => MapSize.Large,
                            _ => MapSize.Small
                        };
                        break;
                    case "--help":
                    case "-h":
                        Console.WriteLine("CatanRoguelike sim-runner (narrow-core driver)");
                        Console.WriteLine("  --runs N          games, seeds SeedStart..+N-1 (default 1)");
                        Console.WriteLine("  --seed-start N    first seed (default 1)");
                        Console.WriteLine("  --timeout-ms N    per-run wall clock (default 5000)");
                        Console.WriteLine("  --max-steps N     per-run action cap (default 300)");
                        Console.WriteLine("  --max-days N      stop after this day (default 12)");
                        Console.WriteLine("  --map small|medium|large");
                        Environment.Exit(0);
                        break;
                }
            }
            return o;
        }
    }

    internal sealed class RunBox
    {
        public volatile string Phase = "";
        public volatile int Days;
        public volatile int Steps;
        public volatile int PlayerVp;
        public volatile int AiVp;
        public volatile int ElapsedMs;
    }

    internal sealed class RunResult
    {
        public int seed { get; set; }
        public string status { get; set; }
        public int days { get; set; }
        public int steps { get; set; }
        public string winner { get; set; }
        public int playerVp { get; set; }
        public int aiVp { get; set; }
        public int playerBonusVp { get; set; }
        public int aiBonusVp { get; set; }
        public string phase { get; set; }
        public int ms { get; set; }
        public string error { get; set; }
    }

    internal sealed class Summary
    {
        public int runs { get; set; }
        public string driver { get; set; }
        public int timeoutMs { get; set; }
        public int maxSteps { get; set; }
        public int maxDays { get; set; }
        public int ok { get; set; }
        public int timeout { get; set; }
        public int crash { get; set; }
        public int capped { get; set; }
        public int winsHuman { get; set; }
        public int winsAi { get; set; }
        public int unfinished { get; set; }
        public double avgDays { get; set; }
        public List<RunResult> results { get; set; }
    }

    /// <summary>
    /// Narrow Core driver. Full GameController placement (PlaceSettlement / AI
    /// GetValidSettlementSpots) stalls inside VertexGraph.VertexDistance once any
    /// building exists. This driver still runs setup + N night/day cycles:
    /// ConfirmRunSetup (AI first piece), inject remaining setup, BeginNight,
    /// SkipNightCard (rolls/events/cards/production/shop), then a day-advance that
    /// skips AI placement. Timeouts and step/day caps always fire.
    /// </summary>
    internal static class MatchDriver
    {
        public static RunResult Play(int seed, Options opts, RunBox box)
        {
            var sw = Stopwatch.StartNew();
            var result = new RunResult
            {
                seed = seed,
                status = "ok",
                winner = "none",
                phase = "start"
            };

            try
            {
                var game = new GameController(seed, opts.MapSize);
                Snapshot(game, box, 0, sw);

                Step(game, box, sw, opts, () => game.SelectMap(opts.MapSize));
                Step(game, box, sw, opts, () => game.SelectLeader(PickLeader(seed)));

                var uniques = (UniqueBuildingId[])Enum.GetValues(typeof(UniqueBuildingId));
                int u0 = Math.Abs(seed) % uniques.Length;
                int u1 = (u0 + 1) % uniques.Length;
                Step(game, box, sw, opts, () => game.ToggleDraftUnique(uniques[u0]));
                Step(game, box, sw, opts, () => game.ToggleDraftUnique(uniques[u1]));
                Step(game, box, sw, opts, () => game.ConfirmRunSetup());
                Step(game, box, sw, opts, () => InjectRemainingSetup(game));

                while (true)
                {
                    Snapshot(game, box, box.Steps, sw);
                    EnsureNotOverTime(sw, opts);

                    if (game.State.Phase == GamePhase.GameOver)
                    {
                        result.status = "ok";
                        break;
                    }

                    if (box.Steps >= opts.MaxSteps)
                    {
                        result.status = "max_steps";
                        result.error = "hit max-steps cap";
                        break;
                    }

                    if (game.State.Board.DayNumber > opts.MaxDays)
                    {
                        result.status = "max_days";
                        result.error = "hit max-days cap";
                        break;
                    }

                    AdvanceOnce(game);
                    box.Steps++;
                }

                Finish(result, game, box, sw);
            }
            catch (TimeoutException)
            {
                result.status = "timeout";
                result.error = "wall-clock timeout";
                result.phase = box.Phase;
                result.days = box.Days;
                result.steps = box.Steps;
                result.playerVp = box.PlayerVp;
                result.aiVp = box.AiVp;
                result.ms = (int)sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                result.status = "crash";
                result.error = ex.GetType().Name + ": " + ex.Message;
                result.phase = box.Phase;
                result.days = box.Days;
                result.steps = box.Steps;
                result.playerVp = box.PlayerVp;
                result.aiVp = box.AiVp;
                result.ms = (int)sw.ElapsedMilliseconds;
            }

            return result;
        }

        private static void Step(GameController game, RunBox box, Stopwatch sw, Options opts, Action action)
        {
            EnsureNotOverTime(sw, opts);
            Snapshot(game, box, box.Steps, sw);
            action();
            box.Steps++;
            Snapshot(game, box, box.Steps, sw);
        }

        private static void EnsureNotOverTime(Stopwatch sw, Options opts)
        {
            if (sw.ElapsedMilliseconds >= opts.TimeoutMs)
                throw new TimeoutException("per-run wall-clock timeout");
        }

        private static void Snapshot(GameController game, RunBox box, int steps, Stopwatch sw)
        {
            box.Phase = game.State.Phase.ToString();
            box.Days = game.State.Board.DayNumber;
            box.Steps = steps;
            box.PlayerVp = game.State.PlayerVictoryPoints;
            box.AiVp = game.State.AiVictoryPoints;
            box.ElapsedMs = (int)sw.ElapsedMilliseconds;
        }

        private static void Finish(RunResult result, GameController game, RunBox box, Stopwatch sw)
        {
            Snapshot(game, box, box.Steps, sw);
            result.days = game.State.Board.DayNumber;
            result.steps = box.Steps;
            result.playerVp = game.State.PlayerVictoryPoints;
            result.aiVp = game.State.AiVictoryPoints;
            result.playerBonusVp = game.State.PlayerBonusVictoryPoints;
            result.aiBonusVp = game.State.AiBonusVictoryPoints;
            result.phase = game.State.Phase.ToString();
            result.ms = (int)sw.ElapsedMilliseconds;
            if (game.State.Winner == PlayerId.Human) result.winner = "human";
            else if (game.State.Winner == PlayerId.Ai) result.winner = "ai";
            else result.winner = "none";
        }

        private static LeaderId PickLeader(int seed)
        {
            var all = (LeaderId[])Enum.GetValues(typeof(LeaderId));
            return all[Math.Abs(seed) % all.Length];
        }

        private static void AdvanceOnce(GameController game)
        {
            switch (game.State.Phase)
            {
                case GamePhase.NightPlayCard:
                    if (game.State.TodayRolls.Count == 0 && game.State.TomorrowRolls.Count > 0)
                        game.State.TodayRolls = new Dictionary<ResourceType, int>(game.State.TomorrowRolls);
                    game.SkipNightCard();
                    break;

                case GamePhase.DayPlayerActions:
                    TryBuyFirstAffordableDeal(game);
                    AdvanceDayWithoutAiPlacement(game);
                    break;

                case GamePhase.LevelUpChoice:
                    if (game.State.PendingLevelUpChoices.Count > 0)
                        game.ChooseLevelUpPerk(game.State.PendingLevelUpChoices[0]);
                    else
                        AdvanceDayWithoutAiPlacement(game);
                    break;

                case GamePhase.SetupAiSettlement1:
                case GamePhase.SetupAiSettlement2:
                case GamePhase.SetupAiRoad1:
                case GamePhase.SetupAiRoad2:
                    game.RunAiSetupStep();
                    break;

                default:
                    throw new InvalidOperationException("unhandled phase " + game.State.Phase);
            }
        }

        private static void TryBuyFirstAffordableDeal(GameController game)
        {
            if (game.State.ShopDeals == null || game.State.ShopDeals.Count == 0)
                return;
            foreach (var deal in game.State.ShopDeals)
            {
                if (game.BuyShopDeal(deal))
                    return;
            }
        }

        /// <summary>
        /// Mirrors GameController.EndDay without Ai.ExecuteDayTurn / placement scans.
        /// </summary>
        private static void AdvanceDayWithoutAiPlacement(GameController game)
        {
            var state = game.State;
            game.Events.ClearDailyEventEffects(state);
            state.Board.DisabledRoads.Clear();

            if (state.AiEmbargoDaysLeft > 0)
            {
                state.AiEmbargoDaysLeft--;
                if (state.AiEmbargoDaysLeft <= 0) state.AiShopEmbargo = null;
            }
            else
                state.AiShopEmbargo = null;

            state.Board.DayNumber++;

            if (RunProgression.ShouldOfferLevelUp(state))
            {
                state.PendingLevelUpChoices.Clear();
                state.PendingLevelUpChoices.AddRange(
                    RunProgression.GenerateLevelUpChoices(state, new Random(state.Board.DayNumber + 17)));
                if (state.PendingLevelUpChoices.Count > 0)
                {
                    state.Phase = GamePhase.LevelUpChoice;
                    state.StatusMessage = "Level up! Choose a perk.";
                    return;
                }
            }

            var winner = VictoryCalculator.CheckWinner(state);
            if (winner.HasValue)
            {
                state.Winner = winner;
                state.Phase = GamePhase.GameOver;
                state.StatusMessage = winner == PlayerId.Human ? "You win!" : "AI wins!";
                return;
            }

            game.BeginNight();
        }

        private static void InjectRemainingSetup(GameController game)
        {
            int humanSettlements = 0;
            foreach (var kv in game.State.Board.VertexBuildings)
            {
                if (kv.Value.owner == PlayerId.Human)
                    humanSettlements++;
            }

            foreach (var vertex in EnumerateVertices(game.State.Board))
            {
                if (humanSettlements >= 2) break;
                if (!IsFreeAndNotAdjacent(game.State.Board, vertex))
                    continue;
                game.State.Board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);
                TryAttachRoad(game.State.Board, vertex, PlayerId.Human);
                humanSettlements++;
            }

            int aiSettlements = 0;
            foreach (var kv in game.State.Board.VertexBuildings)
            {
                if (kv.Value.owner == PlayerId.Ai)
                    aiSettlements++;
            }

            foreach (var vertex in EnumerateVertices(game.State.Board))
            {
                if (aiSettlements >= 2) break;
                if (!IsFreeAndNotAdjacent(game.State.Board, vertex))
                    continue;
                game.State.Board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Ai);
                TryAttachRoad(game.State.Board, vertex, PlayerId.Ai);
                aiSettlements++;
            }

            VictoryCalculator.RefreshVictoryPoints(game.State);
            game.BeginNight();
        }

        private static bool IsFreeAndNotAdjacent(BoardState board, Vertex vertex)
        {
            if (board.VertexBuildings.ContainsKey(vertex))
                return false;

            foreach (var existing in board.VertexBuildings.Keys)
            {
                if (existing.Equals(vertex))
                    return false;
                foreach (var adj in VertexGraph.GetAdjacentVertices(existing))
                {
                    if (adj.Equals(vertex))
                        return false;
                }
            }

            return true;
        }

        private static void TryAttachRoad(BoardState board, Vertex vertex, PlayerId player)
        {
            foreach (var adj in VertexGraph.GetAdjacentVertices(vertex))
            {
                var edge = new Edge(vertex, adj);
                if (board.Roads.ContainsKey(edge))
                    continue;
                board.Roads[edge] = player;
                return;
            }
        }

        private static IEnumerable<Vertex> EnumerateVertices(BoardState board)
        {
            var seen = new HashSet<Vertex>();
            foreach (var hex in board.Tiles.Keys)
            {
                for (int c = 0; c < 6; c++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(hex, c));
                    if (seen.Add(v))
                        yield return v;
                }
            }
        }
    }
}
