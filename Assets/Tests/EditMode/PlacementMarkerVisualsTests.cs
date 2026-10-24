using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using static CatanRoguelike.Tests.PlayerPieceTestUtil;

namespace CatanRoguelike.Tests
{
    public class PlacementMarkerVisualsTests
    {
        private const float HexTopY = 0.15f;
        private const float RoadLength = 1.1f;
        private static readonly Color SettlementGhostColor = new(0.2f, 1f, 0.3f, 0.55f);
        private static readonly Color RoadGhostColor = new(1f, 0.85f, 0.1f, 0.55f);
        private static readonly Color CityGhostColor = new(1f, 0.5f, 0.9f, 0.6f);

        private GameObject _testRoot;

        [SetUp]
        public void SetUp() => _testRoot = new GameObject("PlacementMarkerVisualsTests");

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
                Object.DestroyImmediate(_testRoot);
        }

        private static void AssertGhostSlots(Renderer renderer, Color ghostColor)
        {
            var wall = Slot(renderer, PlayerPieceVisuals.WallMaterialName);
            Assert.AreEqual(ghostColor, wall.color);
            foreach (var m in renderer.sharedMaterials)
            {
                Assert.GreaterOrEqual(m.renderQueue, 3000, $"{m.name} should be transparent");
                Assert.AreEqual(ghostColor.a, m.color.a, 0.001f, $"{m.name} alpha");
            }
        }

        [Test]
        public void CreateSettlementGhost_IsTheHouseMesh_GhostTinted_NoColliders()
        {
            var ghost = PlacementMarkerVisuals.CreateSettlementGhost(
                _testRoot.transform, Vector3.zero, SettlementGhostColor, HexTopY);

            Assert.AreEqual(PlacementMarkerVisuals.SettlementGhostName, ghost.name);
            var filter = SingleMesh(ghost, PlayerPieceVisuals.BodyPartName);
            AssertClosed(filter.sharedMesh);
            Assert.AreNotEqual("Sphere", filter.sharedMesh.name);
            Assert.AreEqual(0, ghost.GetComponentsInChildren<Collider>().Length);
            AssertGhostSlots(filter.GetComponent<Renderer>(), SettlementGhostColor);
        }

        [Test]
        public void CreateCityGhost_IsTheCityMesh_LargerThanSettlementGhost()
        {
            var settlement = PlacementMarkerVisuals.CreateSettlementGhost(
                _testRoot.transform, Vector3.zero, SettlementGhostColor, HexTopY);
            var city = PlacementMarkerVisuals.CreateCityGhost(
                _testRoot.transform, Vector3.zero, CityGhostColor, HexTopY);

            Assert.AreEqual(PlacementMarkerVisuals.CityGhostName, city.name);
            var cityFilter = SingleMesh(city, PlayerPieceVisuals.BodyPartName);
            var settlementFilter = SingleMesh(settlement, PlayerPieceVisuals.BodyPartName);
            Assert.Greater(ScaledModelSize(cityFilter).x, ScaledModelSize(settlementFilter).x * 1.4f);
            Assert.AreEqual(0, city.GetComponentsInChildren<Collider>().Length);
            AssertGhostSlots(cityFilter.GetComponent<Renderer>(), CityGhostColor);
        }

        [Test]
        public void CreateRoadGhost_IsElongatedPlank_GhostTinted_NoColliders()
        {
            var ghost = PlacementMarkerVisuals.CreateRoadGhost(
                _testRoot.transform, new Vector3(0f, HexTopY, 0f), 0f, RoadLength, RoadGhostColor, HexTopY);

            Assert.AreEqual(PlacementMarkerVisuals.RoadGhostName, ghost.name);
            var filter = SingleMesh(ghost, PlayerPieceVisuals.PlankPartName);
            var bounds = filter.GetComponent<Renderer>().bounds;
            Assert.Greater(bounds.size.z, bounds.size.x * 1.2f);
            Assert.AreEqual(0, ghost.GetComponentsInChildren<Collider>().Length);
            AssertGhostSlots(filter.GetComponent<Renderer>(), RoadGhostColor);
        }
    }
}
