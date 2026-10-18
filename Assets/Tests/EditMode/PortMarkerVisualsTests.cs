using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
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
        public void SeaSlotPort_PierPlanks_TouchCoastCorners()
        {
            bool wasActive = ColonistPresentation.IsActive;
            ColonistPresentation.SetActive(true);
            try
            {
                var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
                var port = PortAccess.DiscoverPorts(board).Find(p => !p.IsGeneric);
                Assert.IsNotNull(port);
                Assert.IsTrue(board.TryGetSeaSlotForPort(port.Vertex, out var seaCoord));

                var marker = PortMarkerVisuals.Create(
                    _root.transform,
                    port.Vertex,
                    HexScale,
                    TileHeight,
                    board,
                    false,
                    port.SpecificResource,
                    WoodColor);

                Assert.IsTrue(
                    SeaFrameSlotLayout.TryGetHarborCoastEdge(
                        board, port.Vertex, seaCoord, HexScale, out _, out MapPlaneXz coastA, out MapPlaneXz coastB));

                var coasts = new[]
                {
                    new Vector3(coastA.X, TileHeight, coastA.Z),
                    new Vector3(coastB.X, TileHeight, coastB.Z)
                };

                int planks = 0;
                foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.gameObject.name.StartsWith("PierPlank"))
                        continue;

                    planks++;
                    PortMarkerVisuals.GetPlankSegmentEnds(renderer.transform, out var start, out var end);
                    float minDist = float.MaxValue;
                    foreach (var coast in coasts)
                    {
                        minDist = Mathf.Min(minDist, HorizontalDistance(start, coast));
                        minDist = Mathf.Min(minDist, HorizontalDistance(end, coast));
                    }

                    Assert.Less(
                        minDist,
                        PortMarkerVisuals.SeaSlotPlankCoastTouchEpsilon,
                        "Pier plank should reach the coast corner (no water gap).");
                }

                Assert.Greater(planks, 0, "Sea-slot port should include pier planks.");
            }
            finally
            {
                ColonistPresentation.SetActive(wasActive);
            }
        }

        [Test]
        public void SeaSlotPort_PierPlankBounds_DoNotIntersectLandPolygons()
        {
            bool wasActive = ColonistPresentation.IsActive;
            ColonistPresentation.SetActive(true);
            try
            {
                var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
                var ports = PortAccess.DiscoverPorts(board);
                Assert.Greater(ports.Count, 0);

                foreach (var port in ports)
                {
                    var marker = PortMarkerVisuals.Create(
                        _root.transform,
                        port.Vertex,
                        HexScale,
                        TileHeight,
                        board,
                        port.IsGeneric,
                        port.IsGeneric ? null : port.SpecificResource,
                        WoodColor);

                    foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
                    {
                        if (!renderer.gameObject.name.StartsWith("PierPlank"))
                            continue;

                        AssertPlankBoundsClearOfLand(board, renderer);
                    }
                }
            }
            finally
            {
                ColonistPresentation.SetActive(wasActive);
            }
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

            var rateTex = ColonistPortRateLabelTextures.Get(false);
            float labelHeightFraction = PortMarkerVisuals.BadgeLabelWidthFractionSpecific
                * rateTex.height / (float)rateTex.width;
            float stackFraction = PortMarkerVisuals.BadgeRateIconDiameterFraction + 0.03f + labelHeightFraction;
            Assert.Less(
                stackFraction,
                0.88f,
                $"2:1 icon + label stack ({stackFraction:P0} of disc) must fit inside the badge disc with margin.");

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

        private static void AssertPlankBoundsClearOfLand(BoardState board, Renderer plankRenderer)
        {
            var center = new MapPlaneXz(
                plankRenderer.transform.position.x,
                plankRenderer.transform.position.z);
            float minDistCenter = SeaFrameSlotLayout.MinDistanceToLandHexPolygons(board, center, HexScale);
            Assert.Greater(minDistCenter, 0.001f, "Pier plank center should stay in open water.");
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
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
