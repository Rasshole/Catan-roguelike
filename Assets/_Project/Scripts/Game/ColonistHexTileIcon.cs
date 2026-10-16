using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistHexTileIcon
    {
        private const float IconDiameter = 0.22f;
        private const float IconY = 1.06f;

        public static void Apply(GameObject tileRoot, HexTileData data)
        {
            if (data.IsDesert)
                return;

            var iconGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            iconGo.name = "ResourceIcon";
            iconGo.transform.SetParent(tileRoot.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, IconY, 0f);
            var lossy = tileRoot.transform.lossyScale;
            float d = IconDiameter / lossy.x;
            float h = 0.04f / lossy.y;
            iconGo.transform.localScale = new Vector3(d, h, d);
            UnityEngine.Object.Destroy(iconGo.GetComponent<Collider>());
            iconGo.GetComponent<Renderer>().sharedMaterial =
                BuiltInMaterials.Create(BoardResourceColors.GetHudSwatch(data.Resource));
        }
    }
}
