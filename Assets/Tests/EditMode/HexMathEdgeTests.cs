using CatanRoguelike.Core.Hex;
using NUnit.Framework;

namespace CatanRoguelike.Tests.EditMode
{
    public class HexMathEdgeTests
    {
        [Test]
        public void GetEdgeFacing_MidpointLiesBetweenHexAndNeighbor()
        {
            var hex = new HexCoord(0, 0);
            for (int d = 0; d < 6; d++)
            {
                var neighbor = HexMath.GetNeighbor(hex, d);
                var edge = HexMath.GetEdgeFacing(hex, d);
                var (mx, mz) = HexMath.EdgeMidpoint(edge, 1f);
                var (hx, hz) = HexMath.ToWorldPosition(hex, 1f);
                var (nx, nz) = HexMath.ToWorldPosition(neighbor, 1f);

                float vx = nx - hx;
                float vz = nz - hz;
                float dot = (mx - hx) * vx + (mz - hz) * vz;
                float lenSq = vx * vx + vz * vz;

                Assert.Greater(dot, 0.05f, $"Edge midpoint should be past hex center toward neighbor {d}.");
                Assert.Less(dot, lenSq - 0.05f, $"Edge midpoint should not reach neighbor center for direction {d}.");
            }
        }
    }
}
