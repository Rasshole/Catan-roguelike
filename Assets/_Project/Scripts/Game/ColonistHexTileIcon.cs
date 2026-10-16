using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconWorldDiameter = 0.44f;
        private const float IconLocalY = 1.03f;

        public static void Apply(GameObject tileRoot, HexTileData data)
        {
            if (data.IsDesert)
                return;

            var iconGo = new GameObject("ResourceIcon");
            iconGo.transform.SetParent(tileRoot.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, IconLocalY, 0f);
            iconGo.transform.localRotation = Quaternion.identity;

            var lossy = tileRoot.transform.lossyScale;
            float diameter = IconWorldDiameter / lossy.x;
            float thin = 0.06f / lossy.y;
            iconGo.transform.localScale = new Vector3(diameter, thin, diameter);

            var meshFilter = iconGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var renderer = iconGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(BoardResourceColors.GetHudSwatch(data.Resource));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
