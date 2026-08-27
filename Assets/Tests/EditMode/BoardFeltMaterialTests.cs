using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class BoardFeltMaterialTests
    {
        private Material _material;

        [SetUp]
        public void SetUp() => _material = BoardFeltMaterial.Create();

        [TearDown]
        public void TearDown()
        {
            if (_material != null)
                Object.DestroyImmediate(_material);
        }

        [Test]
        public void Create_UsesFeltAlbedoConstants()
        {
            Assert.AreEqual(BoardFeltMaterial.FeltAlbedo, _material.color);
            Assert.AreEqual(BoardFeltMaterial.Smoothness, _material.GetFloat("_Glossiness"), 0.001f);
            Assert.AreEqual(BoardFeltMaterial.Metallic, _material.GetFloat("_Metallic"), 0.001f);
        }

        [Test]
        public void Create_IsDistinctFromWoodAndSea()
        {
            Assert.AreNotEqual(BoardFeltMaterial.FeltAlbedo, BoardSurfaceMaterial.WoodAlbedo);
            Assert.AreNotEqual(BoardFeltMaterial.FeltAlbedo, BoardWaterMaterial.SeaAlbedo);
        }

        [Test]
        public void Create_IsMatteFeltGreen()
        {
            Assert.Less(_material.GetFloat("_Glossiness"), 0.1f);
            Assert.Greater(_material.color.g, _material.color.r);
            Assert.Greater(_material.color.g, _material.color.b);
        }
    }
}
