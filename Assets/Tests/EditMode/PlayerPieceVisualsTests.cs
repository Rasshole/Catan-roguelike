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

            Assert.AreEqual(PlayerPieceVisuals.WallColor(PlayerPieceVisuals.HumanColor), wall.color);
            Assert.Less(MaxRgb(roof.color), MaxRgb(wall.color) - 0.1f);
            Assert.Less(MaxRgb(edge.color), MaxRgb(roof.color) - 0.1f);
            Assert.Less(roof.GetFloat("_Glossiness"), 0.2f);
        }

        [Test]
        public void Roofs_AreTwoTone_LitPlaneNearPlayerColour_ShadedPlaneDarker()
        {
            foreach (var piece in new[] { Settlement(), City() })
            {
                var renderer = SingleMesh(piece, PlayerPieceVisuals.BodyPartName).GetComponent<MeshRenderer>();
                var lit = Slot(renderer, PlayerPieceVisuals.RoofMaterialName).color;
                var shade = Slot(renderer, PlayerPieceVisuals.RoofShadeMaterialName).color;
                Assert.Greater(MaxRgb(lit), 0.8f * MaxRgb(PlayerPieceVisuals.HumanColor), piece.name + " roof reads as the player colour");
                Assert.Less(MaxRgb(shade), MaxRgb(lit) - 0.15f, piece.name + " ridge splits into two tones");
            }
        }

        [Test]
        public void AiPieces_UseRedPlayerColour()
        {
            var piece = PlayerPieceVisuals.CreateCity(
                _testRoot.transform, Vector3.zero, PlayerPieceVisuals.AiColor, HexTopY);
            var renderer = piece.GetComponentInChildren<MeshRenderer>();
            var wall = Slot(renderer, PlayerPieceVisuals.WallMaterialName).color;
            Assert.AreEqual(PlayerPieceVisuals.WallColor(PlayerPieceVisuals.AiColor), wall);
            Assert.Greater(wall.r, wall.b + 0.3f, "AI pieces read red");
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
        public void ColonistMode_BuildingsAreNearlyTopDown_WithASmallLeanTowardScreenTop()
        {
            Assert.That(PlayerPieceVisuals.ColonistLeanDegrees, Is.InRange(5f, 15f),
                "pieces are seen from above (roofs first) with only a hint of wall");
            var body = Settlement().transform.Find(PlayerPieceVisuals.BodyPartName);
            float tilt = Vector3.Angle(body.up, Vector3.up);
            Assert.AreEqual(PlayerPieceVisuals.ColonistLeanDegrees, tilt, 1f);
            var cameraUp = ColonistFlatBoardQuad.GetColonistCameraUpOnBoard();
            Assert.Greater(
                Vector3.Dot(body.up, cameraUp),
                0.8f * Mathf.Sin(PlayerPieceVisuals.ColonistLeanDegrees * Mathf.Deg2Rad),
                "the small lean goes toward screen-top");
        }

        [Test]
        public void Buildings_AreAboutOneSixthToOneFifthOfHexWidth_CityLarger()
        {
            float hex = PlayerPieceVisuals.FlatHexWidth(ColonistBoardVisualScale.ReferenceHexScale);
            float settlement = ScaledModelSize(SingleMesh(Settlement(), PlayerPieceVisuals.BodyPartName)).x / hex;
            float city = ScaledModelSize(SingleMesh(City(), PlayerPieceVisuals.BodyPartName)).x / hex;
            Assert.That(settlement, Is.InRange(1f / 6.2f, 1f / 4.9f));
            Assert.That(city, Is.InRange(settlement * 1.3f, 0.32f));
        }

        [Test]
        public void Buildings_YawIsStablePerVertex_AndVariesAcrossVertices()
        {
            var a1 = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform, new Vector3(1.3f, 0f, 2.1f), PlayerPieceVisuals.HumanColor, HexTopY);
            var a2 = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform, new Vector3(1.3f, 0f, 2.1f), PlayerPieceVisuals.AiColor, HexTopY);
            Assert.That(Quaternion.Angle(
                a1.transform.Find(PlayerPieceVisuals.BodyPartName).rotation,
                a2.transform.Find(PlayerPieceVisuals.BodyPartName).rotation), Is.LessThan(0.01f));

            var yaws = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 12; i++)
            {
                float yaw = PlayerPieceVisuals.BuildingYawDegrees(new Vector3(i * 1.04f, 0f, i * 0.6f))
                    + PlayerPieceVisuals.ColonistYawDegrees;
                yaws.Add(Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 10f));
                // Always 18-50 deg off gable-on (mod 180) so the house keeps its 3/4 read.
                float folded = Mathf.Repeat(yaw, 180f);
                float offGable = Mathf.Min(folded, 180f - folded);
                Assert.That(offGable, Is.InRange(17.9f, 50.1f));
            }

            Assert.Greater(yaws.Count, 5, "pieces on different vertices should face different ways");
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
            // Runs past both vertex centres so consecutive roads overlap at the shared vertex.
            Assert.GreaterOrEqual(bounds.size.z, 1.1f + bounds.size.x * 0.9f);
        }
    }
}
