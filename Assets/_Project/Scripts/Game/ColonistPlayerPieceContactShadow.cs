using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>Small soft ground tint directly under a piece (colonist top-down readability).</summary>
    internal static class ColonistPlayerPieceContactShadow
    {
        private static readonly Color ShadowTint = new(0.06f, 0.09f, 0.06f, 0.14f);

        public static void Attach(Transform pieceRoot, float hexTopY, float footprintDiameter)
        {
            if (pieceRoot == null)
                return;

            var existing = pieceRoot.Find("ContactShadow");
            if (existing != null)
                ColonistSceneDestroy.SafeDestroy(existing.gameObject);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "ContactShadow";
            go.transform.SetParent(pieceRoot, false);
            float localY = hexTopY - pieceRoot.position.y + 0.002f;
            go.transform.localPosition = new Vector3(0f, localY, 0f);
            float d = Mathf.Clamp(footprintDiameter * 0.72f, 0.14f, 0.55f);
            go.transform.localScale = new Vector3(d, 0.008f, d);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(ShadowTint, zWrite: false);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ColonistSceneDestroy.SafeDestroy(go.GetComponent<Collider>());
        }
    }
}
