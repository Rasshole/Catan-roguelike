using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class BoardSurfaceMaterialTests
    {
        private const float MaxAlbedoChannel = 0.55f;
        private const float MaxSmoothness = 0.2f;

        [Test]
        public void Create_IsDarkMatteWood()
        {
            var material = BoardSurfaceMaterial.Create();
            float maxRgb = Mathf.Max(material.color.r, material.color.g, material.color.b);

            Assert.Less(maxRgb, MaxAlbedoChannel);
            Assert.Less(material.GetFloat("_Glossiness"), MaxSmoothness);
            Assert.AreEqual(BoardSurfaceMaterial.Metallic, material.GetFloat("_Metallic"), 0.001f);
        }
    }
}
