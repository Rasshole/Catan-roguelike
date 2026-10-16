using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconWorldDiameter = 0.38f;
        private const float IconLocalY = 1.04f;

        public static void Apply(GameObject tileRoot, HexTileData data)
        {
            if (data.IsDesert)
                return;

            var iconGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            iconGo.name = "ResourceIcon";
            iconGo.transform.SetParent(tileRoot.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, IconLocalY, 0f);
            iconGo.transform.localRotation = Quaternion.identity;
            var lossy = tileRoot.transform.lossyScale;
            float d = IconWorldDiameter / lossy.x;
            float h = 0.08f / lossy.y;
            iconGo.transform.localScale = new Vector3(d, h, d);
            UnityEngine.Object.Destroy(iconGo.GetComponent<Collider>());
            var renderer = iconGo.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(BoardResourceColors.GetHudSwatch(data.Resource));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
