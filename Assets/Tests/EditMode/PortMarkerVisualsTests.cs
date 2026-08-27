using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class PortMarkerVisualsTests
    {
        private const float HexScale = 1.2f;
        private const float TileHeight = 0.15f;
        private static readonly Color WoodColor = new(0.2f, 0.55f, 0.2f);

        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PortMarkerVisualsTests_Root");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Create_Generic_HasExpectedParts_AboveHexTop_WithSharedMaterials()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var port = PortAccess.DiscoverPorts(board).Find(p => p.IsGeneric);
            Assert.IsNotNull(port);

            var marker = PortMarkerVisuals.Create(
                _root.transform,
                port.Vertex,
                HexScale,
                TileHeight,
                true,
                null,
                Color.white);

            Assert.AreEqual("Port_Generic", marker.name);
            Assert.IsNotNull(marker.transform.Find("PierPlank"));
            Assert.IsNotNull(marker.transform.Find("PierPost"));
            Assert.IsNull(marker.transform.Find("ResourceFlag"));

            AssertMinWorldYAboveHexTop(marker);
            AssertSharedMaterialsAssigned(marker);
        }

        [Test]
        public void Create_Specific_HasResourceFlag_WithDistinctColor()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var ports = PortAccess.DiscoverPorts(board);
            var genericPort = ports.Find(p => p.IsGeneric);
            var specificPort = ports.Find(p => !p.IsGeneric);
            Assert.IsNotNull(genericPort);
            Assert.IsNotNull(specificPort);

            var genericMarker = PortMarkerVisuals.Create(
                _root.transform,
                genericPort.Vertex,
                HexScale,
                TileHeight,
                true,
                null,
                Color.white);

            var specificMarker = PortMarkerVisuals.Create(
                _root.transform,
                specificPort.Vertex,
                HexScale,
                TileHeight,
                false,
                specificPort.SpecificResource,
                WoodColor);

            var flag = specificMarker.transform.Find("ResourceFlag");
            Assert.IsNotNull(flag);

            var genericPlank = genericMarker.transform.Find("PierPlank").GetComponent<Renderer>();
            var specificFlag = flag.GetComponent<Renderer>();
            Assert.IsNotNull(genericPlank.sharedMaterial);
            Assert.IsNotNull(specificFlag.sharedMaterial);
            Assert.AreNotEqual(genericPlank.sharedMaterial.color, specificFlag.sharedMaterial.color);

            AssertMinWorldYAboveHexTop(specificMarker);
            AssertSharedMaterialsAssigned(specificMarker);
        }

        private static void AssertMinWorldYAboveHexTop(GameObject marker)
        {
            float minY = float.PositiveInfinity;
            foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
                minY = Mathf.Min(minY, renderer.bounds.min.y);

            Assert.Greater(minY, TileHeight);
        }

        private static void AssertSharedMaterialsAssigned(GameObject marker)
        {
            foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
                Assert.IsNotNull(renderer.sharedMaterial);
        }
    }
}
