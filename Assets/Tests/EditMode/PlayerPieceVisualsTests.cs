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
        private const float RidgePeakEpsilon = 0.012f;
        private const float MaxSettlementHeightAboveHex = 1.05f;
        private const float MinCityToSettlementHeightRatio = 1.38f;
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

        [Test]
        public void CreateSettlement_HasBodyAndRoofParts()
        {
            var piece = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.HumanColor,
                HexTopY);

            Assert.AreEqual(PlayerPieceVisuals.SettlementName, piece.name);
            if (PlayerPieceVisuals.UsesKenneyModels)
            {
                Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.KenneyModelChildName));
                Assert.Greater(piece.GetComponentsInChildren<Renderer>().Length, 0);
                return;
            }

            Assert.GreaterOrEqual(piece.transform.childCount, 4);
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
            if (PlayerPieceVisuals.UsesKenneyModels)
            {
                Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.KenneyModelChildName));
                Assert.Greater(piece.GetComponentsInChildren<Renderer>().Length, 0);
                return;
            }

            Assert.GreaterOrEqual(piece.transform.childCount, 5);
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
            if (PlayerPieceVisuals.UsesKenneyModels)
            {
                Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.KenneyModelChildName));
                return;
            }

            Assert.AreEqual(1, piece.transform.childCount);
            Assert.NotNull(piece.transform.Find(PlayerPieceVisuals.PlankPartName));
        }

        [Test]
        public void CreateSettlement_RoofRidgeCoversBodyTop()
        {
            if (PlayerPieceVisuals.UsesKenneyModels)
                return;

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
            var towerRoofLeft = piece.transform.Find(PlayerPieceVisuals.TowerRoofLeftPartName).GetComponent<Renderer>();
            var towerRidge = piece.transform.Find(PlayerPieceVisuals.TowerRoofRidgePartName).GetComponent<Renderer>();

            AssertRoofRidgeCoversTop(upperRenderer, towerRoofLeft, piece, towerRidge);
        }

        [Test]
        public void CreateSettlement_RoofUsesLowGlossMaterial()
        {
            var piece = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.AiColor,
                HexTopY);

            var roofRenderer = piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName).GetComponent<Renderer>();

            Assert.Less(roofRenderer.sharedMaterial.GetFloat("_Glossiness"), 0.1f);
            Assert.AreEqual(0f, roofRenderer.sharedMaterial.GetFloat("_Metallic"));
        }

        [Test]
        public void CreateSettlement_RidgeTopCoversRoofPanelPeaks()
        {
            var piece = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.AiColor,
                HexTopY);

            var roofLeftRenderer = piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName).GetComponent<Renderer>();
            var roofRightRenderer = piece.transform.Find(PlayerPieceVisuals.RoofRightPartName).GetComponent<Renderer>();
            var ridgeRenderer = piece.transform.Find(PlayerPieceVisuals.RoofRidgePartName).GetComponent<Renderer>();

            float panelPeakY = Mathf.Max(roofLeftRenderer.bounds.max.y, roofRightRenderer.bounds.max.y);
            Assert.GreaterOrEqual(ridgeRenderer.bounds.max.y, panelPeakY - RidgePeakEpsilon);
        }

        [Test]
        public void CreateSettlement_RoofHasLitAndShadedFaces()
        {
            var piece = PlayerPieceVisuals.CreateSettlement(
                _testRoot.transform,
                Vector3.zero,
                PlayerPieceVisuals.HumanColor,
                HexTopY);

            var roofLeftRenderer = piece.transform.Find(PlayerPieceVisuals.RoofLeftPartName).GetComponent<Renderer>();
            var roofRightRenderer = piece.transform.Find(PlayerPieceVisuals.RoofRightPartName).GetComponent<Renderer>();
            var ridgeRenderer = piece.transform.Find(PlayerPieceVisuals.RoofRidgePartName).GetComponent<Renderer>();

            float litRgb = MaxRgb(roofLeftRenderer.sharedMaterial.color);
            float shadeRgb = MaxRgb(roofRightRenderer.sharedMaterial.color);
            float ridgeRgb = MaxRgb(ridgeRenderer.sharedMaterial.color);

            Assert.Greater(litRgb, shadeRgb);
            Assert.Less(ridgeRgb, shadeRgb);
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
            Assert.Less(maxY - HexTopY, MaxSettlementHeightAboveHex);
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

            float settlementHeight = settlementMaxY - HexTopY;
            float cityHeight = cityMaxY - HexTopY;
            Assert.Greater(cityHeight, settlementHeight * MinCityToSettlementHeightRatio);
            Assert.Greater(cityMinY, HexTopY + MinBaseAboveHex);
            Assert.Less(cityHeight, MaxCityHeightAboveHex);
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
            GameObject piece,
            Renderer ridgeRendererOverride = null)
        {
            var ridgeTransform = ridgeRendererOverride != null
                ? ridgeRendererOverride.transform
                : piece.transform.Find(PlayerPieceVisuals.RoofRidgePartName);
            Assert.NotNull(ridgeTransform);

            var ridgeRenderer = ridgeRendererOverride ?? ridgeTransform.GetComponent<Renderer>();
            var bodyBounds = bodyRenderer.bounds;
            var ridgeBounds = ridgeRenderer.bounds;
            var roofRightRenderer = roofLeftRenderer.transform.parent.Find(
                roofLeftRenderer.gameObject.name.Contains("Tower")
                    ? PlayerPieceVisuals.TowerRoofRightPartName
                    : PlayerPieceVisuals.RoofRightPartName).GetComponent<Renderer>();

            Assert.Less(MaxRgb(ridgeRenderer.sharedMaterial.color), MaxRgb(roofLeftRenderer.sharedMaterial.color));

            float panelMinX = Mathf.Min(roofLeftRenderer.bounds.min.x, roofRightRenderer.bounds.min.x);
            float panelMaxX = Mathf.Max(roofLeftRenderer.bounds.max.x, roofRightRenderer.bounds.max.x);
            float panelMinZ = Mathf.Min(roofLeftRenderer.bounds.min.z, roofRightRenderer.bounds.min.z);
            float panelMaxZ = Mathf.Max(roofLeftRenderer.bounds.max.z, roofRightRenderer.bounds.max.z);
            Assert.LessOrEqual(panelMinX, bodyBounds.min.x + 0.02f);
            Assert.GreaterOrEqual(panelMaxX, bodyBounds.max.x - 0.02f);
            Assert.LessOrEqual(panelMinZ, bodyBounds.min.z + 0.02f);
            Assert.GreaterOrEqual(panelMaxZ, bodyBounds.max.z - 0.02f);
            Assert.Greater(ridgeBounds.min.y, bodyBounds.max.y + MinRidgeGapAboveBodyTop - 0.01f);

            float panelPeakY = Mathf.Max(roofLeftRenderer.bounds.max.y, roofRightRenderer.bounds.max.y);
            Assert.GreaterOrEqual(ridgeBounds.max.y, panelPeakY);
        }
    }
}
