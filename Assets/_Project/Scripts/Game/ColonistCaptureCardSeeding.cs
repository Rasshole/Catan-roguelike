using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;

namespace CatanRoguelike.Game
{
    /// <summary>Deterministic roguelike hand for colonist HUD captures and review screenshots.</summary>
    public static class ColonistCaptureCardSeeding
    {
        public static readonly IReadOnlyList<CardId> DemoHand = new[]
        {
            CardId.RoadBuilder,
            CardId.Knight,
            CardId.YearOfPlenty,
            CardId.Monopoly,
            CardId.MasterBuilder
        };

        public static void ForceDemoHandForCapture(GameState state)
        {
            if (!ColonistPresentation.IsActive || state == null)
                return;

            state.PlayerHand.Clear();
            foreach (var id in DemoHand)
                state.PlayerHand.Add(id);
        }
    }
}
