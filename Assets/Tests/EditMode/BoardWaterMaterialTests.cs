using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class BoardWaterMaterialTests
    {
        [Test]
        public void Create_UsesSeaAlbedoConstants()
        {
            var material = BoardWaterMaterial.Create();

            Assert.AreEqual(BoardWaterMaterial.SeaAlbedo, material.color);
            Assert.AreEqual(BoardWaterMaterial.Smoothness, material.GetFloat("_Glossiness"), 0.001f);
            Assert.AreEqual(BoardWaterMaterial.Metallic, material.GetFloat("_Metallic"), 0.001f);
        }

        [Test]
        public void Create_IsDarkerThanWood()
        {
            var water = BoardWaterMaterial.Create();
            float waterMax = Mathf.Max(water.color.r, water.color.g, water.color.b);
            float woodMax = Mathf.Max(
                BoardSurfaceMaterial.WoodAlbedo.r,
                BoardSurfaceMaterial.WoodAlbedo.g,
                BoardSurfaceMaterial.WoodAlbedo.b);

            Assert.Less(waterMax, woodMax);
        }
    }
}
