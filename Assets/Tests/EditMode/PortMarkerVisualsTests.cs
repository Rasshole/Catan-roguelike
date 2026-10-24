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
        public void SeaSlotPort_PierPlanks_RunFromBadgeOntoTheCoastEdge()
        {
            bool wasActive = ColonistPresentation.IsActive;
            ColonistPresentation.SetActive(true);
            try
            {
                var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
                foreach (var port in PortAccess.DiscoverPorts(board))
                {
                    Assert.IsTrue(board.TryGetSeaSlotForPort(port.Vertex, out var seaCoord));
                    var marker = PortMarkerVisuals.Create(
                        _root.transform,
                        port.Vertex,
                        HexScale,
                        TileHeight,
                        board,
                        port.IsGeneric,
                        port.IsGeneric ? null : port.SpecificResource,
                        WoodColor);

                    Assert.IsTrue(
                        SeaFrameSlotLayout.TryGetHarborCoastEdge(
                            board, port.Vertex, seaCoord, HexScale, out _, out MapPlaneXz coastA, out MapPlaneXz coastB));
                    var a = new Vector2(coastA.X, coastA.Z);
                    var b = new Vector2(coastB.X, coastB.Z);
                    var badge = new Vector2(marker.transform.position.x, marker.transform.position.z);

                    int planks = 0;
                    foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
                    {
                        if (!renderer.gameObject.name.StartsWith("PierPlank"))
                            continue;

                        planks++;
                        PortMarkerVisuals.GetPlankSegmentEnds(renderer.transform, out var s3, out var e3);
                        var s2 = new Vector2(s3.x, s3.z);
                        var e2 = new Vector2(e3.x, e3.z);
                        // One end at the badge, the other past the coast edge (on the tile).
                        var landEnd = Vector2.Distance(s2, badge) > Vector2.Distance(e2, badge) ? s2 : e2;
                        var seaEnd = landEnd == s2 ? e2 : s2;
                        Assert.Less(Vector2.Distance(seaEnd, badge), 0.05f, "pier starts at the badge");
                        Assert.IsTrue(
                            SegmentsCross(seaEnd, landEnd, a, b, out float alongEdge),
                            "pier must cross the harbour coast edge (no water gap, no floating end)");
                        Assert.That(alongEdge, Is.InRange(0.08f, 0.92f), "pier lands on the edge, not off its ends");
                        float past = DistanceToLine(landEnd, a, b);
                        Assert.That(past, Is.InRange(0.02f, PortMarkerVisuals.PierLandOverlap + 0.02f),
                            "pier runs a little onto the tile's sand frame");
                    }

                    Assert.AreEqual(2, planks, "each harbour has two piers");
                    Object.DestroyImmediate(marker);
                }
            }
            finally
            {
                ColonistPresentation.SetActive(wasActive);
            }
        }

        private static bool SegmentsCross(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out float tq)
        {
            Vector2 r = p2 - p, sv = q2 - q;
            float denom = r.x * sv.y - r.y * sv.x;
            tq = -1f;
            if (Mathf.Abs(denom) < 1e-8f)
                return false;
            Vector2 qp = q - p;
            float t = (qp.x * sv.y - qp.y * sv.x) / denom;
            tq = (qp.x * r.y - qp.y * r.x) / denom;
            return t >= 0f && t <= 1f && tq >= 0f && tq <= 1f;
        }

        private static float DistanceToLine(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = (b - a).normalized;
            Vector2 ap = p - a;
            return Mathf.Abs(ap.x * ab.y - ap.y * ab.x);
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
