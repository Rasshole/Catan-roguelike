using CatanRoguelike.Core.Cards;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney HUD / resource sprites for roguelike hand card art.</summary>
    public static class ColonistRoguelikeCardArt
    {
        private static Sprite _wheat;

        public static Sprite GetIcon(CardId cardId)
        {
            switch (cardId)
            {
                case CardId.RoadBuilder:
                    return ColonistKenneyHudIcons.BuildRoad;
                case CardId.Knight:
                    return ColonistKenneyHudIcons.Knight;
                case CardId.YearOfPlenty:
                    return GetWheatIcon();
                case CardId.Monopoly:
                    return ColonistKenneyHudIcons.Resources;
                case CardId.MasterBuilder:
                    return ColonistKenneyHudIcons.BuildSettlement;
                default:
                    return ColonistKenneyHudIcons.DevCard;
            }
        }

        private static Sprite GetWheatIcon()
        {
            if (_wheat != null)
                return _wheat;
            var tex = Resources.Load<Texture2D>("Art/Icons/Wheat");
            if (tex == null)
                return ColonistKenneyHudIcons.Resources;
            _wheat = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _wheat;
        }
    }
}
