using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney Medieval RTS top-down sprites flat on terrain hex tops.</summary>
    internal static class ColonistHexTileTopDownDecor
    {
        private const string TextureRoot = "Art/Board/TopDownDecor";
        private const int RenderQueue = 3195;
        private const float LiftAboveTop = 0.032f;

        public static Texture2D LoadSprite(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
                return null;
            return Resources.Load<Texture2D>($"{TextureRoot}/{fileNameWithoutExtension}");
        }

        public static bool TryPlace(
            GameObject hexRoot,
            Transform propsParent,
            float hexScale,
            Vector3 localXZ,
            string spriteName,
            float diameterInHexUnits,
            float yawDeg,
            string objectName,
            float tokenKeepOut,
            float maxCenterRadius)
        {
            var texture = LoadSprite(spriteName);
            if (texture == null || hexRoot == null || propsParent == null)
                return false;

            float worldSize = Mathf.Max(hexScale * diameterInHexUnits, hexScale * 0.12f);
            var holder = new GameObject(objectName);
            holder.transform.SetParent(propsParent, false);
            holder.transform.localPosition = localXZ + Vector3.up * (HexPrismMesh.TopY + LiftAboveTop);
            holder.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            ColonistHexTilePropPrimitives.CreateSpriteShadowDisc(
                holder.transform,
                Vector3.zero,
                hexScale,
                worldSize * 0.42f);

            ColonistFlatBoardQuad.Create(
                holder.transform,
                "Sprite",
                Vector3.zero,
                worldSize,
                worldSize,
                texture,
                RenderQueue,
                zWrite: false,
                alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);

            if (!ColonistHexTileNatureDecor.NudgePropOutwardUntilClear(hexRoot, holder.transform, tokenKeepOut, hexScale))
            {
                ColonistSceneDestroy.SafeDestroy(holder);
                return false;
            }

            var flat = new Vector2(holder.transform.localPosition.x, holder.transform.localPosition.z);
            if (flat.magnitude > maxCenterRadius)
            {
                ColonistSceneDestroy.SafeDestroy(holder);
                return false;
            }

            return true;
        }
    }
}
