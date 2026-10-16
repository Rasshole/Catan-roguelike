using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests.EditMode
{
    public class SeaFrameSlotLayoutTests
    {
        private const float HexScale = 1.2f;

        [Test]
        public void LargeFishermenBoard_PortAndFishSlots_AreDistinctSeaHexes()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            var ports = PortAccess.DiscoverPorts(board);

            Assert.AreEqual(ports.Count, board.PortVertexToSeaSlot.Count,
                "Each port should own one sea hex slot.");

            var owned = new HashSet<HexCoord>(board.PortVertexToSeaSlot.Values);
            Assert.AreEqual(board.PortVertexToSeaSlot.Count, owned.Count,
                "Port sea slots must be unique.");

            foreach (var fishCoord in board.SeaFishGrounds.Keys)
                Assert.IsFalse(owned.Contains(fishCoord),
                    $"Fish ground at {fishCoord} must not share a port sea slot.");

            var land = new HashSet<HexCoord>(board.Tiles.Keys);
            foreach (var coord in owned)
                Assert.IsFalse(land.Contains(coord), $"Port slot {coord} must not be land.");
            foreach (var coord in board.SeaFishGrounds.Keys)
                Assert.IsFalse(land.Contains(coord), $"Fish slot {coord} must not be land.");
        }

        [Test]
        public void PortSeaSlots_UseCoastEdgeThatContainsPortVertex()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            var ports = PortAccess.DiscoverPorts(board);

            foreach (var port in ports)
            {
                var vertex = VertexGraph.Canonicalize(port.Vertex);
                Assert.IsTrue(board.PortVertexToSeaSlot.TryGetValue(vertex, out var sea),
                    $"Missing sea slot for port {vertex}.");

                Assert.IsTrue(
                    SeaFrameSlotLayout.TryGetHarborCoastEdge(
                        board, vertex, sea, HexScale, out _, out var coastA, out var coastB),
                    $"Harbor coast edge should resolve for port {vertex} and sea {sea}.");

                var canon = VertexGraph.Canonicalize(vertex);
                var (vx, vz) = HexMath.VertexToWorldPosition(canon, HexScale);
                const float tol = 1e-3f;
                bool matchesA = System.Math.Abs(coastA.X - vx) < tol && System.Math.Abs(coastA.Z - vz) < tol;
                bool matchesB = System.Math.Abs(coastB.X - vx) < tol && System.Math.Abs(coastB.Z - vz) < tol;
                Assert.IsTrue(
                    matchesA || matchesB,
                    $"Sea slot {sea} for port {vertex} is not on the port's coast edge.");
            }
        }

        [Test]
        public void PortBadgeCenters_SitInSeaSlots_NotOnLand()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            var ports = PortAccess.DiscoverPorts(board);
            float radius = SeaFrameSlotLayout.DefaultBadgeDiscRadius;

            foreach (var port in ports)
            {
                Assert.IsTrue(
                    SeaFrameSlotLayout.TryGetBadgeCenter(
                        board, port.Vertex, HexScale, radius, out MapPlaneXz center),
                    $"Badge center should resolve for port {port.Vertex}.");

                foreach (var landCoord in board.Tiles.Keys)
                {
                    Assert.IsFalse(
                        SeaFrameSlotLayout.DiscIntersectsHexPolygon(center, radius, landCoord, HexScale),
                        $"Port badge at {center} overlaps land hex {landCoord}.");
                }
            }
        }

        [Test]
        public void FishGrounds_AreSpreadAroundTheSeaRing()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            Assert.GreaterOrEqual(board.SeaFishGrounds.Count, 4);

            MapPlaneXz center = MapPlaneXz.Zero;
            int count = 0;
            foreach (var coord in board.Tiles.Keys)
            {
                var (x, z) = HexMath.ToWorldPosition(coord, 1f);
                center += new MapPlaneXz(x, z);
                count++;
            }

            center /= count;
            var angles = new List<float>();
            foreach (var coord in board.SeaFishGrounds.Keys)
            {
                var (x, z) = HexMath.ToWorldPosition(coord, 1f);
                angles.Add((float)System.Math.Atan2(z - center.Z, x - center.X));
            }

            angles.Sort();
            float minGap = float.MaxValue;
            for (int i = 0; i < angles.Count; i++)
            {
                int next = (i + 1) % angles.Count;
                float gap = angles[next] - angles[i];
                if (next == 0)
                    gap = (float)(2.0 * System.Math.PI) + angles[0] - angles[^1];
                minGap = System.Math.Min(minGap, gap);
            }

            Assert.Greater(minGap, 0.35f, "Fish grounds should not cluster on one arc of the sea ring.");
        }
    }
}
