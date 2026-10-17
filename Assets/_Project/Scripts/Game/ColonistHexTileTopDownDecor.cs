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
            holder.transform.localRotation = Quaternion.identity;

            // Open sprites (fence rails) would show the dark disc through their gaps.
            if (spriteName != "fence")
                ColonistHexTilePropPrimitives.CreateSpriteShadowDisc(
                holder.transform,
                Vector3.zero,
                hexScale,
                worldSize * 0.42f);

            var quad = ColonistFlatBoardQuad.Create(
                holder.transform,
                "Sprite",
                Vector3.zero,
                worldSize,
                worldSize,
                texture,
                RenderQueue,
                zWrite: false,
                alignment: ColonistFlatBoardQuad.AlignmentMode.ColonistCamera);
            // BoardPitchOnly ignored the yaw and showed every sprite turned 90 deg under the yawed table
            // camera (kiln/cactus lying sideways, fence vertical). Face the camera, then spin in-plane:
            // Kenney art is lit from the top-left, so keep it near-upright except crop fields.
            float spin = spriteName.StartsWith("wheat_field") ? yawDeg : Mathf.Repeat(yawDeg, 24f) - 12f;
            quad.transform.rotation = Quaternion.AngleAxis(spin, Vector3.up) * quad.transform.rotation;

            // Circular footprint: the art is roughly round, but the yawed square quad's AABB is up to
            // 1.41x wider, so the bounds-based nudge rejected every mid/large sprite (no windmill/peak).
            float artRadius = worldSize * 0.42f;
            var start = new Vector2(localXZ.x, localXZ.z);
            if (start.sqrMagnitude < 1e-6f)
                start = Vector2.right * 0.01f;
            float minCenter = tokenKeepOut + artRadius;
            // Keep big sprites inside the hex flats (inner radius = 0.866 * hexScale).
            maxCenterRadius = Mathf.Min(maxCenterRadius, hexScale * 0.866f - artRadius * 0.8f);
            if (minCenter > maxCenterRadius)
            {
                ColonistSceneDestroy.SafeDestroy(holder);
                return false;
            }

            float r = Mathf.Clamp(start.magnitude, minCenter, maxCenterRadius);
            float baseAngle = Mathf.Atan2(start.y, start.x);
            // Walk around the ring (alternating sides) until the sprite does not overlap earlier ones.
            for (int attempt = 0; attempt < 14; attempt++)
            {
                float step = (attempt + 1) / 2 * 26f * Mathf.Deg2Rad * (attempt % 2 == 0 ? 1f : -1f);
                float rr = Mathf.Lerp(minCenter, maxCenterRadius, attempt % 3 == 0 ? Mathf.InverseLerp(minCenter, maxCenterRadius, r) : attempt % 3 == 1 ? 0.9f : 0.2f);
                var p = new Vector2(Mathf.Cos(baseAngle + step), Mathf.Sin(baseAngle + step)) * rr;
                if (!OverlapsPlacedSprite(propsParent, holder.transform, p, artRadius))
                {
                    holder.transform.localPosition = new Vector3(p.x, holder.transform.localPosition.y, p.y);
                    return true;
                }
            }

            ColonistSceneDestroy.SafeDestroy(holder);
            return false;
        }

        private static bool OverlapsPlacedSprite(Transform propsParent, Transform self, Vector2 p, float radius)
        {
            foreach (Transform other in propsParent)
            {
                if (other == self)
                    continue;
                var quad = other.Find("Sprite");
                if (quad == null)
                    continue;
                float otherRadius = quad.localScale.x * 0.42f;
                var q = new Vector2(other.localPosition.x, other.localPosition.z);
                if ((q - p).magnitude < (radius + otherRadius) * 0.82f)
                    return true;
            }

            return false;
        }
    }
}
