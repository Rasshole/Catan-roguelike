using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class PlayerPieceVisualsTests
    {
        private const float HexTopY = 0.15f;
        private const float MinBaseAboveHex = 0.01f;
        private const float MinRidgeGapAboveBodyTop = 0.03f;
        private const float MaxSettlementHeight = 0.50f;
        private const float MinCityHeight = 0.25f;
        private const float MaxCityHeight = 0.72f;
        private const float MinRoadBaseAboveHex = 0.005f;
        private const float MaxRoadHeight = 0.28f;

        private GameObject _testRoot;

        [SetUp]
        public void SetUp() => _testRoot = new GameObject("PlayerPieceVisualsTests");

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
                Object.DestroyImmediate(_testRoot);
        }

        [Test]
        public void CreateSettlement_HasBodyAndRoofParts()
        {
            var piece = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.HumanColor,
                HexTopY);

            Assert.AreEqual(PlayerPieceVisuals.SettlementName, piece.name);
            Assert.AreEqual(4, piece.transform.childCount);
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.BodyPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofRightPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofRidgePartName));
        }

        [Test]
        public void CreateCity_HasBodyUpperStoreyAndRoofParts()
        {
            var piece = PlayerPieceVisuals.CreateCity(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.AiColor,
                HexTopY);

            Assert.AreEqual(PlayerPieceVisuals.CityName, piece.name);
            Assert.AreEqual(5, piece.transform.childCount);
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.BodyPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.UpperStoreyPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofRightPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofRidgePartName));
        }

        [Test]
        public void CreateRoad_HasPlankPart()
        {
            var piece = PlayerPieceVisuals.CreateRoad(
                _testRoot.transform,
                Vector3.zero,
                30f,
                1.1f,
                PlayerPieceVisuals.HumanColor,
                HexTopY);

            Assert.AreEqual(PlayerPieceVisuals.RoadName, piece.name);
            Assert.AreEqual(1, piece.transform.childCount);
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.PlankPartName));
        }

        [Test]
        public void CreateSettlement_RoofRidgeCoversBodyTop()
        {
            var piece = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.HumanColor,
                HexTopY);

            var bodyRenderer = piece.transform.Find(PlayerPieceVisuals.BodyPartName).GetComponent<Renderer>();
            var roofLeftRenderer = piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName).GetComponent<Renderer>();

            AssertRoofRidgeCoversTop(bodyRenderer, roofLeftRenderer, piece);
        }

        [Test]
        public void CreateCity_RoofRidgeCoversUpperStoreyTop()
        {
            var piece = PlayerPieceVisuals.CreateCity(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.AiColor,
                HexTopY);

            var upperRenderer = piece.transform.Find(PlayerPieceVisuals.UpperStoreyPartName).GetComponent<Renderer>();
            var roofLeftRenderer = piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName).GetComponent<Renderer>();

            AssertRoofRidgeCoversTop(upperRenderer, roofLeftRenderer, piece);
        }

        [Test]
        public void CreateSettlement_RoofIsDarkerThanBody()
        {
            var piece = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.HumanColor,
                HexTopY);

            var bodyRenderer = piece.transform.Find(PlayerPieceVisuals.BodyPartName).GetComponent<Renderer>();
            var roofRenderer = piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName).GetComponent<Renderer>();

            float bodyMaxRgb = MaxRgb(bodyRenderer.sharedMaterial.color);
            float roofMaxRgb = MaxRgb(roofRenderer.sharedMaterial.color);

            Assert.Less(roofMaxRgb, bodyMaxRgb);
        }

        [Test]
        public void CreateSettlement_SitsAboveHexTop()
        {
            var piece = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.HumanColor,
                HexTopY);

            float minY = GetWorldMinY(piece);
            float maxY = GetWorldMaxY(piece);

            Assert.Greater(minY, HexTopY + MinBaseAboveHex);
            Assert.Less(maxY, HexTopY + MaxSettlementHeight);
        }

        [Test]
        public void CreateCity_IsTallerThanSettlement()
        {
            var settlement = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.HumanColor,
                HexTopY);
            var city = PlayerPieceVisuals.CreateCity(
                _testRoot.transform,
                new Vector3(2f, 0f, 0f),
                PlayerPieceVisuals.AiColor,
                HexTopY);

            float settlementMaxY = GetWorldMaxY(settlement);
            float cityMinY = GetWorldMinY(city);
            float cityMaxY = GetWorldMaxY(city);

            Assert.Greater(cityMaxY, settlementMaxY);
            Assert.Greater(cityMinY, HexTopY + MinBaseAboveHex);
            Assert.Greater(cityMaxY, HexTopY + MinCityHeight);
            Assert.Less(cityMaxY, HexTopY + MaxCityHeight);
        }

        [Test]
        public void CreateRoad_SitsOnHexRim()
        {
            var piece = PlayerPieceVisuals.CreateRoad(
                _testRoot.transform,
                Vector3.zero,
                0f,
                1.1f,
                PlayerPieceVisuals.AiColor,
                HexTopY);

            float minY = GetWorldMinY(piece);
            float maxY = GetWorldMaxY(piece);

            Assert.Greater(minY, HexTopY + MinRoadBaseAboveHex);
            Assert.Less(maxY, HexTopY + MaxRoadHeight);
        }

        private static float GetWorldMinY(GameObject root)
        {
            float minY = float.MaxValue;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                minY = Mathf.Min(minY, renderer.bounds.min.y);

            return minY;
        }

        private static float GetWorldMaxY(GameObject root)
        {
            float maxY = float.MinValue;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                maxY = Mathf.Max(maxY, renderer.bounds.max.y);

            return maxY;
        }

        private static float MaxRgb(Color color) =>
            Mathf.Max(color.r, Mathf.Max(color.g, color.b));

        private static void AssertRoofRidgeCoversTop(
            Renderer bodyRenderer,
            Renderer roofLeftRenderer,
            GameObject piece)
        {
            var ridgeTransform = piece.transform.Find(PlayerPieceVisuals.RoofRidgePartName);
            Assert.NotNull(ridgeTransform);

            var ridgeRenderer = ridgeTransform.GetComponent<Renderer>();
            var bodyBounds = bodyRenderer.bounds;
            var ridgeBounds = ridgeRenderer.bounds;

            Assert.AreSame(roofLeftRenderer.sharedMaterial, ridgeRenderer.sharedMaterial);
            Assert.Less(MaxRgb(ridgeRenderer.sharedMaterial.color), MaxRgb(bodyRenderer.sharedMaterial.color));

            Assert.LessOrEqual(ridgeBounds.min.x, bodyBounds.min.x);
            Assert.GreaterOrEqual(ridgeBounds.max.x, bodyBounds.max.x);
            Assert.LessOrEqual(ridgeBounds.min.z, bodyBounds.min.z);
            Assert.GreaterOrEqual(ridgeBounds.max.z, bodyBounds.max.z);
            Assert.Greater(ridgeBounds.min.y, bodyBounds.max.y + MinRidgeGapAboveBodyTop);
        }
    }
}
