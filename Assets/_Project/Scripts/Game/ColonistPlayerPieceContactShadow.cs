using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>Soft ground contact shadow for board pieces (replaces harsh realtime smudges).</summary>
    internal static class ColonistPlayerPieceContactShadow
    {
        private static readonly Color ShadowTint = new(0.08f, 0.12f, 0.08f, 0.22f);

        public static void Attach(Transform pieceRoot, float hexTopY)
        {
            if (pieceRoot == null)
                return;

            var existing = pieceRoot.Find("ContactShadow");
            if (existing != null)
                ColonistSceneDestroy.SafeDestroy(existing.gameObject);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "ContactShadow";
            go.transform.SetParent(pieceRoot, false);
            float y = hexTopY - pieceRoot.position.y + 0.004f;
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(0.20f, 0.012f, 0.20f);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(ShadowTint, zWrite: false);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ColonistSceneDestroy.SafeDestroy(go.GetComponent<Collider>());
        }
    }
}
