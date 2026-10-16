using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Procedural sand-dune bumps on desert hexes (placeholder until painted art).</summary>
    public static class ColonistDesertDuneDecor
    {
        private static readonly Color DuneHighlight = new(0.92f, 0.84f, 0.62f);
        private static readonly Color DuneShadow = new(0.78f, 0.68f, 0.48f);

        public static void Apply(GameObject tileRoot, float hexScale)
        {
            var root = new GameObject("DesertDunes");
            root.transform.SetParent(tileRoot.transform, false);
            float r = hexScale * HexMath.OuterRadius * 0.22f;
            CreateDune(root.transform, new Vector3(-0.18f * hexScale, HexPrismMesh.TopY + 0.04f, 0.12f * hexScale), r * 1.1f, DuneShadow);
            CreateDune(root.transform, new Vector3(0.22f * hexScale, HexPrismMesh.TopY + 0.05f, -0.08f * hexScale), r * 0.95f, DuneHighlight);
            CreateDune(root.transform, new Vector3(0.02f * hexScale, HexPrismMesh.TopY + 0.03f, -0.28f * hexScale), r * 0.75f, DuneShadow);
        }

        private static void CreateDune(Transform parent, Vector3 localPos, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dune";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            float sy = radius * 0.35f;
            go.transform.localScale = new Vector3(radius * 2f, sy * 2f, radius * 1.6f);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(color);
            ColonistSceneDestroy.SafeDestroy(go.GetComponent<Collider>());
        }
    }
}
