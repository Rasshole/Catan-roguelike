using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Procedural sand-dune bumps on desert hexes (placeholder until painted art).</summary>
    public static class ColonistDesertDuneDecor
    {
        private const int DuneRenderQueue = 3195;
        private static readonly Color DuneHighlight = new(0.98f, 0.86f, 0.55f);
        private static readonly Color DuneMid = new(0.93f, 0.78f, 0.48f);
        private static readonly Color DuneShadow = new(0.84f, 0.66f, 0.38f);

        public static void Apply(GameObject tileRoot, float hexScale)
        {
            var root = new GameObject("DesertDunes");
            root.transform.SetParent(tileRoot.transform, false);
            root.transform.SetAsLastSibling();

            float top = HexPrismMesh.TopY + 0.06f;
            float r = hexScale * HexMath.OuterRadius * 0.28f;

            CreateDuneRidge(
                root.transform,
                new Vector3(-0.34f * hexScale, top, 0.1f * hexScale),
                new Vector3(0.3f * hexScale, top + 0.04f, -0.24f * hexScale),
                r * 1.1f,
                DuneShadow,
                DuneMid);
            CreateDuneRidge(
                root.transform,
                new Vector3(0.14f * hexScale, top + 0.02f, 0.3f * hexScale),
                new Vector3(-0.2f * hexScale, top + 0.05f, -0.04f * hexScale),
                r,
                DuneMid,
                DuneHighlight);
            CreateDuneRidge(
                root.transform,
                new Vector3(0.36f * hexScale, top, -0.14f * hexScale),
                new Vector3(-0.1f * hexScale, top + 0.03f, 0.22f * hexScale),
                r * 0.85f,
                DuneShadow,
                DuneHighlight);
        }

        private static void CreateDuneRidge(
            Transform parent,
            Vector3 startLocal,
            Vector3 endLocal,
            float radius,
            Color shadowColor,
            Color highlightColor)
        {
            const int segments = 6;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 pos = Vector3.Lerp(startLocal, endLocal, t);
                float crest = Mathf.Sin(t * Mathf.PI);
                pos.y += crest * radius * 0.55f;
                var color = Color.Lerp(shadowColor, highlightColor, crest);
                CreateDuneBump(parent, pos, radius * (0.6f + crest * 0.4f), color, t * 32f);
            }
        }

        private static void CreateDuneBump(Transform parent, Vector3 localPos, float radius, Color color, float yawDegrees)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dune";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            float sy = radius * 0.32f;
            go.transform.localScale = new Vector3(radius * 2.8f, sy * 2.2f, radius * 1.35f);
            var renderer = go.GetComponent<Renderer>();
            var mat = BuiltInMaterials.CreateUnlit(color);
            mat.renderQueue = DuneRenderQueue;
            renderer.sharedMaterial = mat;
            ColonistSceneDestroy.SafeDestroy(go.GetComponent<Collider>());
        }
    }
}
