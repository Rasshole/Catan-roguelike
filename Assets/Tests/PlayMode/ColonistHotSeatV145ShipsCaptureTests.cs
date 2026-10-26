using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Tests.PlayMode
{
    /// <summary>
    /// v145 Seafarers smoke test on the Islands map: boats and the pirate on the board, the Ship / Move ship
    /// buttons, sea placement circles, building and sailing a ship by clicking, and the pirate pick on a 7.
    /// Set COLONIST_HOTSEAT_SHOT_DIR to write review captures (v145-*.png).
    /// </summary>
    public class ColonistHotSeatV145ShipsCaptureTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        private static IEnumerator LoadScene()
        {
            if (!File.Exists(ColonistScenePath))
                Assert.Inconclusive("Colonist prototype scene missing.");
            Screen.SetResolution(GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight, false);
            yield return null;
            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            yield return loadOp;
            for (int i = 0; i < 4; i++)
                yield return null;
        }

        private static IEnumerator ReadyHud(GameManager gameManager, System.Action<RunHudController> done)
        {
            RunHudController hud = null;
            for (int i = 0; i < 120; i++)
            {
                gameManager.EnsureRunHudReady();
                hud = gameManager.GetComponent<RunHudController>();
                if (hud != null && hud.IsHudCaptureReady())
                    break;
                yield return null;
            }

            Assert.IsNotNull(hud);
            hud.Refresh(gameManager.Controller.State);
            yield return Settle(hud);
            done(hud);
        }

        private static IEnumerator Settle(RunHudController hud, int frames = 4)
        {
            Canvas.ForceUpdateCanvases();
            if (hud.HudCanvas != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hud.HudCanvas.GetComponent<RectTransform>());
            for (int i = 0; i < frames; i++)
                yield return null;
        }

        private static void MaybeWrite(RunHudController hud, string file)
        {
            string dir = System.Environment.GetEnvironmentVariable("COLONIST_HOTSEAT_SHOT_DIR");
            if (string.IsNullOrEmpty(dir))
                return;
            Directory.CreateDirectory(dir);
            var tex = GameSceneCapture.CaptureMainCameraWithHudComposite(hud);
            Assert.IsNotNull(tex);
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
            Debug.Log($"v145 capture written: {file}");
        }

        private static Button ActionButton(Transform root, string label)
        {
            var b = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x =>
                x.name.StartsWith("Action_") && x.GetComponentInChildren<Text>(true)?.text == label);
            Assert.IsNotNull(b, "action button " + label);
            return b;
        }

        private static bool Drive(HotSeatSession s, System.Func<bool> stop, int guardMax = 20000)
        {
            int guard = 0;
            while (guard++ < guardMax && !s.IsOver)
            {
                s.Tick(0f);
                if (s.PassScreenPending)
                    s.ConfirmPass();
                if (MatchAi.AwaitingHumanAnswer(s.Match))
                {
                    s.Apply(MatchRunner.PendingResponse(s.Match));
                    continue;
                }

                if (s.Acting.IsAi)
                    continue;
                if (stop())
                    return true;
                switch (s.State.Phase)
                {
                    case MatchPhase.Roll:
                        s.Roll();
                        break;
                    case MatchPhase.Main:
                        s.EndTurn();
                        break;
                    default:
                        s.ApplySuggested();
                        break;
                }
            }

            return false;
        }

        private static Edge E(Vertex a, Vertex b) => VertexGraph.GetEdgeBetween(a, b);

        private static IEnumerable<Vertex> Corners(BoardState b) =>
            b.Tiles.Keys.Concat(b.SeaHexes).SelectMany(h => Enumerable.Range(0, 6).Select(c => VertexGraph.Canonicalize(new Vertex(h, c))))
                .Distinct().OrderBy(v => v.Hex.Q).ThenBy(v => v.Hex.R).ThenBy(v => v.CornerIndex);

        /// <summary>A free home-coast corner whose sea path reaches an islet in a few ships.</summary>
        private static (Vertex start, List<Vertex> path) CoastToIslet(MatchController m)
        {
            var b = m.State.Board;
            var islets = new HashSet<HexCoord>(MapPresets.IslandsIsletCoords);
            foreach (var v in Corners(b))
            {
                var hexes = VertexGraph.GetHexesForVertex(v).ToList();
                if (!hexes.Any(b.SetupLandHexes.Contains) || !hexes.Any(b.SeaHexes.Contains)
                    || !m.Placement.CanPlaceSettlement(b, v, m.State.Seats[0].Owner, true))
                    continue;
                var prev = new Dictionary<Vertex, Vertex> { [v] = v };
                var queue = new Queue<Vertex>();
                queue.Enqueue(v);
                while (queue.Count > 0)
                {
                    var u = queue.Dequeue();
                    if (!u.Equals(v) && VertexGraph.GetHexesForVertex(u).Any(islets.Contains))
                    {
                        var path = new List<Vertex> { u };
                        while (!path[0].Equals(v))
                            path.Insert(0, prev[path[0]]);
                        if (path.Count >= 4 && path.Count <= 7)
                            return (v, path);
                        break;
                    }

                    foreach (var n in VertexGraph.GetAdjacentVertices(u))
                    {
                        var c = VertexGraph.Canonicalize(n);
                        if (!prev.ContainsKey(c) && ShipRules.IsShipEdge(b, E(u, c)) && !b.Ships.ContainsKey(ShipRules.Normalize(E(u, c))))
                        {
                            prev[c] = u;
                            queue.Enqueue(c);
                        }
                    }
                }
            }

            return (default, null);
        }

        private static int HexDist(HexCoord a, HexCoord b) =>
            (Mathf.Abs(a.Q - b.Q) + Mathf.Abs(a.R - b.R) + Mathf.Abs(a.Q + a.R - b.Q - b.R)) / 2;

        private static int CountNamed(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Count(t => t.name == name);

        [Timeout(240000)]
        [UnityTest]
        public IEnumerator Islands_ShipsAndPirate_OnTheBoard_BuildSailAndPirateByClicking()
        {
            yield return LoadScene();
            var gm = Object.FindFirstObjectByType<GameManager>();
            RunHudController hud = null;
            yield return ReadyHud(gm, h => hud = h);

            hud.HotSeatClockFrozenForTests = true;
            var config = new MatchConfig { Seed = 1451, MapSize = MapSize.Islands };
            config.Seats.Add(new SeatConfig(SeatKind.Human, "Rasmus"));
            config.Seats.Add(new SeatConfig(SeatKind.Ai, "Astrid"));
            config.Seats.Add(new SeatConfig(SeatKind.Ai, "Bjorn"));
            hud.StartHotSeat(config);
            var s = hud.HotSeatSessionForTests;
            Assert.IsTrue(s.Match.SeafarersOn, "the Islands map has ships");
            Assert.IsTrue(Drive(s, () => s.State.Phase == MatchPhase.Main && s.State.CurrentSeat == 0), "reach my turn");
            var board = s.State.Board;
            var view = Object.FindFirstObjectByType<BoardView>();
            Assert.IsNotNull(view);
            float scale = view.HexScale;

            // Seed the table: my coastal harbour town, Astrid's ship line toward the other islet, the pirate nearby.
            var me = s.State.Seats[0].Owner;
            var (home, path) = CoastToIslet(s.Match);
            Assert.IsNotNull(path, "a coast-to-islet sea lane");
            board.VertexBuildings[home] = (BuildingType.Settlement, me);
            var astrid = s.State.Seats[1].Owner;
            // Astrid sails for the other islet (the one my lane doesn't reach).
            var mine = VertexGraph.GetHexesForVertex(path.Last()).First(MapPresets.IslandsIsletCoords.Contains);
            var other = MapPresets.IslandsIsletCoords.Where(h => HexDist(h, mine) > 2).ToList();
            Assert.IsNotEmpty(other, "two islets");
            float ox = other.Average(h => HexMath.ToWorldPosition(h).x), oz = other.Average(h => HexMath.ToWorldPosition(h).z);
            float Far(Vertex v)
            {
                var (vx, vz) = HexMath.VertexToWorldPosition(v);
                return (vx - ox) * (vx - ox) + (vz - oz) * (vz - oz);
            }

            var astridHome = Corners(board).Where(v => !v.Equals(home)
                    && VertexGraph.GetHexesForVertex(v).Any(board.SetupLandHexes.Contains) && VertexGraph.GetHexesForVertex(v).Any(board.SeaHexes.Contains)
                    && s.Match.Placement.CanPlaceSettlement(board, v, astrid, true)
                    && VertexGraph.VertexDistance(v, home) > 4)
                .OrderBy(Far).First();
            board.VertexBuildings[astridHome] = (BuildingType.Settlement, astrid);
            var lane = new List<Edge>();
            var at = astridHome;
            for (int i = 0; i < 3; i++)
            {
                var next = VertexGraph.GetAdjacentVertices(at).Select(VertexGraph.Canonicalize)
                    .Where(n => ShipRules.CanPlaceShip(board, E(at, n), astrid)).OrderBy(Far).ThenBy(n => n.ToString()).FirstOrDefault();
                if (next.Equals(default(Vertex)) || Far(next) >= Far(at) && i > 0)
                    break;
                var e = ShipRules.Normalize(E(at, next));
                board.Ships[e] = astrid;
                lane.Add(e);
                at = next;
            }

            Assert.IsNotEmpty(lane, "Astrid's ships");
            var pirateHex = ShipRules.EdgeHexes(lane.Last()).Where(board.SeaHexes.Contains)
                .OrderBy(h => HexDist(h, other[0])).First();
            board.PirateHex = pirateHex;
            s.State.Seats[0].Hand = new ResourceBundle { Wood = 3, Sheep = 3, Brick = 1 };
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.AreEqual(lane.Count, CountNamed(view.transform, ShipPieceVisuals.ShipName), "a boat per ship");
            Assert.AreEqual(1, CountNamed(view.transform, ShipPieceVisuals.PirateName), "the pirate ship");

            // Ship button → glowing sea circles → click the lane's first edge.
            ActionButton(hud.HotSeatRootForTests, "Ship").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            var input = hud.HotSeatBoardInputForTests;
            Assert.AreEqual(MatchBuildMode.Ship, input.Mode);
            Assert.Greater(CountNamed(input.HighlightsRootForTests, HotSeatBoardInput.ShipSpotName), 0, "sea placement circles");
            ActionButton(hud.HotSeatRootForTests, "Land");
            MaybeWrite(hud, "v145-ship-spots.png");

            var first = ShipRules.Normalize(E(path[0], path[1]));
            var (fx, fz) = HexMath.EdgeMidpoint(first, scale);
            var built = input.ClickForTests(fx, fz);
            Assert.IsNotNull(built);
            Assert.AreEqual(MatchCommandType.BuildShip, built.Type);
            Assert.AreEqual(me, board.Ships[first]);
            var second = ShipRules.Normalize(E(path[1], path[2]));
            var (sx, sz) = HexMath.EdgeMidpoint(second, scale);
            Assert.AreEqual(MatchCommandType.BuildShip, input.ClickForTests(sx, sz)?.Type, "and the next one out");
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 8);
            Assert.AreEqual(lane.Count + 2, CountNamed(view.transform, ShipPieceVisuals.ShipName));
            MaybeWrite(hud, "v145-ships-and-pirate.png");

            // Next turn: Move ship → pick the open ship → sail it.
            s.State.ShipsBuiltThisTurn.Clear();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            ActionButton(hud.HotSeatRootForTests, "Move ship").onClick.Invoke();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            Assert.AreEqual(MatchBuildMode.MoveShip, input.Mode);
            Assert.IsNull(input.ClickForTests(sx, sz), "step one picks the ship");
            Assert.AreEqual(second, input.MoveFrom);
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            MaybeWrite(hud, "v145-move-ship.png");
            var to = s.Match.ShipMoveTargets(0, second).OrderBy(e => e.ToString()).First();
            var (tx, tz) = HexMath.EdgeMidpoint(to, scale);
            var sail = input.ClickForTests(tx, tz);
            Assert.AreEqual(MatchCommandType.MoveShip, sail?.Type);
            Assert.IsTrue(board.Ships.ContainsKey(ShipRules.Normalize(to)));
            Assert.IsFalse(board.Ships.ContainsKey(second));
            Assert.AreEqual(MatchBuildMode.Auto, input.Mode, "back to building");
            Assert.IsTrue(s.State.ShipMovedThisTurn);

            // A 7: click an empty sea hex to move the pirate.
            s.State.Phase = MatchPhase.MoveRobber;
            s.State.ResumePhase = MatchPhase.Main;
            var empty = board.SeaHexes.Where(h => h != board.PirateHex && !ShipRules.HexEdges(h).Any(board.Ships.ContainsKey))
                .OrderBy(h => HexDist(h, pirateHex)).ThenBy(h => h.Q).ThenBy(h => h.R).First();
            hud.RefreshHotSeatNow();
            yield return Settle(hud);
            var (px, pz) = HexMath.ToWorldPosition(empty, scale);
            var pirate = input.ClickForTests(px, pz);
            Assert.AreEqual(MatchCommandType.MovePirate, pirate?.Type);
            Assert.AreEqual(empty, board.PirateHex);
            hud.RefreshHotSeatNow();
            yield return Settle(hud, 8);
            Assert.AreEqual(1, CountNamed(view.transform, ShipPieceVisuals.PirateName), "one pirate");
            MaybeWrite(hud, "v145-pirate-moved.png");
        }
    }
}
