using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class PlayerPieceVisualsTests
    {
        private const float HexTopY = 0.15f;
        private const float MinBaseAboveHex = 0.01f;
        private const float MaxSettlementHeight = 0.38f;
        private const float MinCityHeight = 0.25f;
        private const float MaxCityHeight = 0.55f;
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
            Assert.AreEqual(3, piece.transform.childCount);
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.BodyPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofRightPartName));
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
            Assert.AreEqual(4, piece.transform.childCount);
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.BodyPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.UpperStoreyPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName));
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.RoofRightPartName));
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
    }
}
