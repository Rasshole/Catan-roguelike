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
        private const float LegacyPlankLength = 0.18f;
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
                board,
                true,
                null,
                Color.white);

            Assert.AreEqual("Port_Generic", marker.name);
            Assert.IsNotNull(marker.transform.Find("PierPlank"));
            Assert.IsNotNull(marker.transform.Find("PierPost"));
            Assert.IsNull(marker.transform.Find("ResourceFlag"));

            AssertMinWorldYAboveHexTop(marker);
            AssertSharedMaterialsAssigned(marker);
            AssertPlankLengthReadableFromTable(marker);
        }

        [Test]
        public void Create_Specific_HasRateIcon_OnBadge()
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
                board,
                true,
                null,
                Color.white);

            var specificMarker = PortMarkerVisuals.Create(
                _root.transform,
                specificPort.Vertex,
                HexScale,
                TileHeight,
                board,
                false,
                specificPort.SpecificResource,
                WoodColor);

            var badge = specificMarker.transform.Find("PortRateBadge");
            Assert.IsNotNull(badge);
            Assert.IsNotNull(badge.Find("RateLabel"));
            var rateIcon = badge.Find("RateLabel/RateIcon");
            if (rateIcon == null)
                rateIcon = badge.Find("RateIcon");
            Assert.IsNotNull(rateIcon, "2:1 port should include a RateIcon on the badge.");
            Assert.IsNull(specificMarker.transform.Find("ResourceFlag"));
            Assert.IsNull(genericMarker.transform.Find("PortRateBadge/RateLabel/RateIcon"));
            Assert.IsNull(genericMarker.transform.Find("PortRateBadge/RateIcon"));

            var iconRenderer = rateIcon.GetComponent<Renderer>();
            var discRenderer = badge.Find("BadgeDisc").GetComponent<Renderer>();
            float discWidth = discRenderer.transform.localScale.x;
            float iconWidth = ColonistFlatBoardQuad.MeasureWorldWidthAlongTextureU(rateIcon);
            float expectedIcon = discWidth * PortMarkerVisuals.BadgeRateIconDiameterFraction;
            Assert.Greater(
                iconWidth,
                expectedIcon * 0.92f,
                $"Rate icon should be ~{PortMarkerVisuals.BadgeRateIconDiameterFraction:P0} of badge disc diameter.");
            Assert.Less(
                iconWidth,
                expectedIcon * 1.08f,
                $"Rate icon should stay near {PortMarkerVisuals.BadgeRateIconDiameterFraction:P0} of disc diameter.");

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

        private static void AssertPlankLengthReadableFromTable(GameObject marker)
        {
            var plank = marker.transform.Find("PierPlank");
            Assert.IsNotNull(plank);

            float plankLength = plank.localScale.z;
            Assert.Greater(plankLength, LegacyPlankLength * 1.5f);
            Assert.Less(plankLength, HexScale * 2.5f);
        }
    }
}
