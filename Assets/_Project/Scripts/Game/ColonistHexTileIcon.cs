using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconWorldDiameter = 0.58f;

        public static void Apply(GameObject tileRoot, HexTileData data)
        {
            if (data.IsDesert)
                return;

            var iconGo = new GameObject("ResourceIcon");
            iconGo.transform.SetParent(tileRoot.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, HexPrismMesh.TopY + 0.06f, 0f);
            iconGo.transform.localRotation = Quaternion.identity;

            var lossy = tileRoot.transform.lossyScale;
            float diameter = IconWorldDiameter / lossy.x;
            float thin = Mathf.Max(0.08f / lossy.y, 0.02f);
            iconGo.transform.localScale = new Vector3(diameter, thin, diameter);

            var meshFilter = iconGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var renderer = iconGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(BoardResourceColors.GetHudSwatch(data.Resource));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial.renderQueue = 3600;
        }
    }
}
