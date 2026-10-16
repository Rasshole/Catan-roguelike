using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class ColonistHexFootprintTests
    {
        private const float HexScale = 1.2f;
        private const float ColonistHeightScale = 0.0375f;

        [Test]
        public void HexTileTransform_DoesNotSquashFootprintOnZ()
        {
            var root = new GameObject("TileRoot");
            root.transform.localScale = new Vector3(1f, ColonistHeightScale, 1f);

            var meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = HexPrismMesh.Create(new HexCoord(0, 0), HexScale);

            float xzAspect = ColonistCaptureProjection.MeasureHexTopWorldXzAspectRatio(root.transform);
            Object.DestroyImmediate(root);

            Assert.That(xzAspect, Is.EqualTo(0.8660254f).Within(0.02f));
        }

        [Test]
        public void HexTileTransform_WithAccidentalZScale_WouldFailFootprintGuard()
        {
            var root = new GameObject("TileRootSquashed");
            root.transform.localScale = new Vector3(1f, ColonistHeightScale, 0.5f);

            var meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = HexPrismMesh.Create(new HexCoord(0, 0), HexScale);

            float xzAspect = ColonistCaptureProjection.MeasureHexTopWorldXzAspectRatio(root.transform);
            Object.DestroyImmediate(root);

            Assert.Greater(xzAspect, 1.5f, "Z scale on the tile transform should widen the footprint ratio.");
        }
    }
}
