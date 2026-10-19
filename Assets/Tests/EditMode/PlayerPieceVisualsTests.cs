using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using static CatanRoguelike.Tests.PlayerPieceTestUtil;

namespace CatanRoguelike.Tests
{
    public class PlayerPieceVisualsTests
    {
        private const float HexTopY = 0.15f;
        private const float MinBaseAboveHex = 0.01f;
        private const float MaxSettlementHeightAboveHex = 1.05f;
        private const float MaxCityHeightAboveHex = 1.65f;
        private const float MinRoadBaseAboveHex = 0.005f;
        private const float MaxRoadHeight = 0.28f;

        private GameObject _testRoot;
        private bool _colonistWasActive;

        [SetUp]
        public void SetUp()
        {
            _colonistWasActive = ColonistPresentation.IsActive;
            ColonistPresentation.SetActive(true);
            _testRoot = new GameObject("PlayerPieceVisualsTests");
        }

        [TearDown]
        public void TearDown()
        {
            ColonistPresentation.SetActive(_colonistWasActive);
            if (_testRoot != null)
                Object.DestroyImmediate(_testRoot);
        }

        private GameObject Settlement() => PlayerPieceVisuals.CreateSettlement(
            _testRoot.transform, Vector3.zero, PlayerPieceVisuals.HumanColor, HexTopY);

        private GameObject City() => PlayerPieceVisuals.CreateCity(
            _testRoot.transform, new Vector3(3f, 0f, 0f), PlayerPieceVisuals.HumanColor, HexTopY);

        [Test]
        public void CreateSettlement_IsOneClosedHouseMeshWithPitchedRoof()
        {
            var piece = Settlement();
            Assert.AreEqual(PlayerPieceVisuals.SettlementName, piece.name);
            var filter = SingleMesh(piece, PlayerPieceVisuals.BodyPartName);
            AssertClosed(filter.sharedMesh);

            var renderer = filter.GetComponent<MeshRenderer>();
            Assert.Greater(RoofTriangles(filter.sharedMesh, renderer.sharedMaterials), 1,
                "settlement needs sloped roof faces");
            // Pentagon prism house: roughly as tall as wide, not a flat slab or a box.
            var size = filter.sharedMesh.bounds.size;
            Assert.That(size.y / size.x, Is.InRange(0.8f, 1.2f));
            Assert.AreEqual(0, piece.GetComponentsInChildren<Collider>().Length);
        }

        [Test]
        public void CreateCity_IsOneClosedMesh_AboutOneAndAHalfSettlementFootprint_AndTaller()
        {
            var settlement = SingleMesh(Settlement(), PlayerPieceVisuals.BodyPartName);
            var city = SingleMesh(City(), PlayerPieceVisuals.BodyPartName);
            AssertClosed(city.sharedMesh);

            var s = ScaledModelSize(settlement);
            var c = ScaledModelSize(city);
            Assert.That(c.x / s.x, Is.InRange(1.4f, 1.6f), "city footprint should be ~1.5x settlement");
            Assert.That(c.y / s.y, Is.InRange(1.6f, 2.1f), "city tower should stand well above a settlement");
            Assert.Greater(RoofTriangles(city.sharedMesh, city.GetComponent<MeshRenderer>().sharedMaterials), 3,
                "city needs roofs on both the long house and the tower");
        }

        [Test]
        public void Pieces_RoofDarkerThanWalls_EdgesDarkest_Matte()
        {
            var renderer = SingleMesh(Settlement(), PlayerPieceVisuals.BodyPartName).GetComponent<MeshRenderer>();
            var wall = Slot(renderer, PlayerPieceVisuals.WallMaterialName);
            var roof = Slot(renderer, PlayerPieceVisuals.RoofMaterialName);
            var edge = Slot(renderer, PlayerPieceVisuals.EdgeMaterialName);

            Assert.AreEqual(PlayerPieceVisuals.HumanColor, wall.color);
            Assert.Less(MaxRgb(roof.color), MaxRgb(wall.color) - 0.1f);
            Assert.Less(MaxRgb(edge.color), MaxRgb(roof.color) - 0.1f);
            Assert.Less(roof.GetFloat("_Glossiness"), 0.2f);
        }

        [Test]
        public void AiPieces_UseRedPlayerColour()
        {
            var piece = PlayerPieceVisuals.CreateCity(
                _testRoot.transform, Vector3.zero, PlayerPieceVisuals.AiColor, HexTopY);
            var renderer = piece.GetComponentInChildren<MeshRenderer>();
            Assert.AreEqual(PlayerPieceVisuals.AiColor, Slot(renderer, PlayerPieceVisuals.WallMaterialName).color);
        }

        [Test]
        public void Buildings_SitAboveHexTop_WithBoundedHeight()
        {
            foreach (var piece in new[] { Settlement(), City() })
            {
                var y = WorldYRange(piece.GetComponentInChildren<MeshFilter>());
                Assert.Greater(y.x, HexTopY + MinBaseAboveHex, piece.name);
                float max = piece.name == PlayerPieceVisuals.CityName
                    ? MaxCityHeightAboveHex
                    : MaxSettlementHeightAboveHex;
                Assert.Less(y.y - HexTopY, max, piece.name);
            }
        }

        [Test]
        public void ColonistMode_BuildingsLeanTowardTheTopDownCamera()
        {
            var body = Settlement().transform.Find(PlayerPieceVisuals.BodyPartName);
            float tilt = Vector3.Angle(body.up, Vector3.up);
            Assert.AreEqual(PlayerPieceVisuals.ColonistLeanDegrees, tilt, 1f);
            var cameraUp = ColonistFlatBoardQuad.GetColonistCameraUpOnBoard();
            Assert.Greater(Vector3.Dot(body.up, cameraUp), 0.5f, "should lean toward screen-top");
        }

        [Test]
        public void ClassicMode_BuildingsStandUpright()
        {
            ColonistPresentation.SetActive(false);
            var body = Settlement().transform.Find(PlayerPieceVisuals.BodyPartName);
            Assert.AreEqual(0f, Vector3.Angle(body.up, Vector3.up), 0.01f);
        }

        [Test]
        public void CreateRoad_IsClosedBevelledPlankOnHexRim()
        {
            var road = PlayerPieceVisuals.CreateRoad(
                _testRoot.transform, new Vector3(1f, HexTopY, 0f), 0f, 1.1f, PlayerPieceVisuals.HumanColor, HexTopY);
            Assert.AreEqual(PlayerPieceVisuals.RoadName, road.name);
            var filter = SingleMesh(road, PlayerPieceVisuals.PlankPartName);
            AssertClosed(filter.sharedMesh);

            var renderer = filter.GetComponent<MeshRenderer>();
            Slot(renderer, PlayerPieceVisuals.EdgeMaterialName);
            var bounds = renderer.bounds;
            Assert.Greater(bounds.min.y, HexTopY + MinRoadBaseAboveHex);
            Assert.Less(bounds.max.y, HexTopY + MaxRoadHeight);
            Assert.Greater(bounds.size.z, bounds.size.x * 4f, "plank runs along the edge (local Z)");
        }
    }
}
