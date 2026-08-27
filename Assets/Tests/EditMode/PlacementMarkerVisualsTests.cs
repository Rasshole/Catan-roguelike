using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void CreateSettlementGhost_HasHouseParts_NoColliders_NotSphere()
        {
            var ghost = PlacementMarkerVisuals.CreateSettlementGhost(
                _testRoot.transform,
                Vector3.zero,
                SettlementGhostColor,
                HexTopY);

            Assert.AreEqual(PlacementMarkerVisuals.SettlementGhostName, ghost.name);
            Assert.NotNull(ghost.transform.Find(PlayerPieceVisuals.BodyPartName));
            Assert.NotNull(ghost.transform.Find(PlayerPieceVisuals.RoofLeftPartName));
            Assert.NotNull(ghost.transform.Find(PlayerPieceVisuals.RoofRidgePartName));
            Assert.AreEqual(0, ghost.GetComponentsInChildren<Collider>().Length);
            AssertSettlementSilhouetteNotSphere(ghost);
        }

        [Test]
        public void CreateSettlementGhost_UsesGhostMaterialColor()
        {
            var ghost = PlacementMarkerVisuals.CreateSettlementGhost(
                _testRoot.transform,
                Vector3.zero,
                SettlementGhostColor,
                HexTopY);

            var bodyRenderer = ghost.transform.Find(PlayerPieceVisuals.BodyPartName).GetComponent<Renderer>();
            AssertGhostMaterialColor(bodyRenderer.sharedMaterial.color, SettlementGhostColor);
        }

        [Test]
        public void CreateCityGhost_HasUpperStoreyAndRoofParts()
        {
            var ghost = PlacementMarkerVisuals.CreateCityGhost(
                _testRoot.transform,
                Vector3.zero,
                CityGhostColor,
                HexTopY);

            Assert.AreEqual(PlacementMarkerVisuals.CityGhostName, ghost.name);
            Assert.NotNull(ghost.transform.Find(PlayerPieceVisuals.UpperStoreyPartName));
            Assert.NotNull(ghost.transform.Find(PlayerPieceVisuals.RoofLeftPartName));
            Assert.AreEqual(0, ghost.GetComponentsInChildren<Collider>().Length);
        }

        [Test]
        public void CreateRoadGhost_HasPlankElongatedAlongEdge()
        {
            var ghost = PlacementMarkerVisuals.CreateRoadGhost(
                _testRoot.transform,
                Vector3.zero,
                0f,
                RoadLength,
                RoadGhostColor,
                HexTopY);

            Assert.AreEqual(PlacementMarkerVisuals.RoadGhostName, ghost.name);

            var plank = ghost.transform.Find(PlayerPieceVisuals.PlankPartName);
            Assert.NotNull(plank);
            Assert.Greater(plank.localScale.z, plank.localScale.x);
            Assert.AreEqual(0, ghost.GetComponentsInChildren<Collider>().Length);

            var plankRenderer = plank.GetComponent<Renderer>();
            AssertGhostMaterialColor(plankRenderer.sharedMaterial.color, RoadGhostColor);
        }

        private static void AssertSettlementSilhouetteNotSphere(GameObject ghost)
        {
            Assert.Greater(ghost.transform.childCount, 1);
            foreach (var filter in ghost.GetComponentsInChildren<MeshFilter>())
            {
                string meshName = filter.sharedMesh != null ? filter.sharedMesh.name : string.Empty;
                Assert.That(meshName, Does.Not.Contain("Sphere"));
            }
        }

        private static void AssertGhostMaterialColor(Color actual, Color expected)
        {
            Assert.AreEqual(expected.r, actual.r, 0.02f);
            Assert.AreEqual(expected.g, actual.g, 0.02f);
            Assert.AreEqual(expected.b, actual.b, 0.02f);
            Assert.AreEqual(expected.a, actual.a, 0.02f);
        }
    }
}
