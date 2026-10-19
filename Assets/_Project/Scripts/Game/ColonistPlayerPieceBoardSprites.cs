using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>Colonist.io-style board sprites from Blender 3/4 renders (readable under table camera).</summary>
    internal static class ColonistPlayerPieceBoardSprites
    {
        private const string TextureRoot = "Art/Board/PlayerPieceSprites";
        private const int RenderQueue = 3210;
        private const float SettlementFootprintOfHex = PlayerPieceVisuals.SettlementFootprintOfHex;
        private const float CityFootprintOfHex = PlayerPieceVisuals.CityFootprintOfHex;

        /// <summary>When true, colonist mode uses sprites instead of runtime primitives.</summary>
        public static bool UseBoardSprites { get; private set; } = true;

        internal static void SetUseBoardSprites(bool use) => UseBoardSprites = use;

        public static bool SpritesAvailable =>
            Load(PlayerId.Human, BuildingType.Settlement) != null
            && Load(PlayerId.Ai, BuildingType.Settlement) != null
            && Load(PlayerId.Human, BuildingType.City) != null
            && Load(PlayerId.Ai, BuildingType.City) != null;

        public static GameObject TryCreateSettlement(
            Transform parent,
            Vector3 worldPosition,
            Color color,
            float hexTopY,
            float hexScale)
        {
            return TryCreateBillboard(
                parent,
                worldPosition,
                hexTopY,
                hexScale,
                PlayerPieceVisuals.SettlementName,
                ResolvePlayer(color),
                BuildingType.Settlement,
                SettlementFootprintOfHex);
        }

        public static GameObject TryCreateCity(
            Transform parent,
            Vector3 worldPosition,
            Color color,
            float hexTopY,
            float hexScale)
        {
            return TryCreateBillboard(
                parent,
                worldPosition,
                hexTopY,
                hexScale,
                PlayerPieceVisuals.CityName,
                ResolvePlayer(color),
                BuildingType.City,
                CityFootprintOfHex);
        }

        private static PlayerId ResolvePlayer(Color color)
        {
            float dHuman = ColorDistance(color, PlayerPieceVisuals.HumanColor);
            float dAi = ColorDistance(color, PlayerPieceVisuals.AiColor);
            return dHuman <= dAi ? PlayerId.Human : PlayerId.Ai;
        }

        private static float ColorDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        private static GameObject TryCreateBillboard(
            Transform parent,
            Vector3 worldPosition,
            float hexTopY,
            float hexScale,
            string rootName,
            PlayerId player,
            BuildingType kind,
            float footprintFraction)
        {
            var texture = Load(player, kind);
            if (texture == null)
                return null;

            float flat = PlayerPieceVisuals.FlatHexWidth(hexScale);
            float footprint = flat * footprintFraction;
            float aspect = texture.height / (float)Mathf.Max(1, texture.width);
            float worldHeight = footprint * Mathf.Max(aspect, 1.05f);
            float worldWidth = footprint;

            var root = new GameObject(rootName);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;

            float lift = hexTopY - worldPosition.y + 0.02f;
            var quad = ColonistFlatBoardQuad.Create(
                root.transform,
                "BoardSprite",
                new Vector3(0f, lift, 0f),
                worldWidth,
                worldHeight,
                texture,
                RenderQueue,
                zWrite: true,
                alignment: ColonistFlatBoardQuad.AlignmentMode.ColonistCamera);

            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return root;
        }

        private static Texture2D Load(PlayerId player, BuildingType kind)
        {
            string playerKey = player == PlayerId.Human ? "Human" : "Ai";
            string kindKey = kind == BuildingType.City ? "City" : "Settlement";
            return Resources.Load<Texture2D>($"{TextureRoot}/{kindKey}{playerKey}");
        }
    }
}
