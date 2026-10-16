using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Procedural sand-dune bumps on desert hexes (placeholder until painted art).</summary>
    public static class ColonistDesertDuneDecor
    {
        private static readonly Color DuneHighlight = new(0.96f, 0.84f, 0.58f);
        private static readonly Color DuneMid = new(0.90f, 0.76f, 0.50f);
        private static readonly Color DuneShadow = new(0.82f, 0.66f, 0.42f);

        public static void Apply(GameObject tileRoot, float hexScale)
        {
            var root = new GameObject("DesertDunes");
            root.transform.SetParent(tileRoot.transform, false);
            float r = hexScale * HexMath.OuterRadius * 0.18f;
            float top = HexPrismMesh.TopY + 0.02f;

            CreateDuneRidge(
                root.transform,
                new Vector3(-0.32f * hexScale, top, 0.08f * hexScale),
                new Vector3(0.28f * hexScale, top + 0.02f, -0.22f * hexScale),
                r * 1.05f,
                DuneShadow,
                DuneMid);
            CreateDuneRidge(
                root.transform,
                new Vector3(0.12f * hexScale, top + 0.01f, 0.26f * hexScale),
                new Vector3(-0.18f * hexScale, top + 0.03f, -0.06f * hexScale),
                r * 0.9f,
                DuneMid,
                DuneHighlight);
            CreateDuneRidge(
                root.transform,
                new Vector3(0.34f * hexScale, top, -0.12f * hexScale),
                new Vector3(-0.08f * hexScale, top + 0.02f, 0.18f * hexScale),
                r * 0.75f,
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
            const int segments = 5;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 pos = Vector3.Lerp(startLocal, endLocal, t);
                float crest = Mathf.Sin(t * Mathf.PI);
                pos.y += crest * radius * 0.45f;
                var color = Color.Lerp(shadowColor, highlightColor, crest);
                CreateDuneBump(parent, pos, radius * (0.55f + crest * 0.35f), color, t * 28f);
            }
        }

        private static void CreateDuneBump(Transform parent, Vector3 localPos, float radius, Color color, float yawDegrees)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dune";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            float sy = radius * 0.28f;
            go.transform.localScale = new Vector3(radius * 2.4f, sy * 2f, radius * 1.1f);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(color);
            ColonistSceneDestroy.SafeDestroy(go.GetComponent<Collider>());
        }
    }
}
