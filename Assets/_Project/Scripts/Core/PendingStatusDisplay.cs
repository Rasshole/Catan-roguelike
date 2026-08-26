using System.Linq;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;

namespace CatanRoguelike.Core
{
    /// <summary>
    /// Pure helpers for IMGUI status lines about pending card effects (Harbor Charter, Embargo, level-up).
    /// </summary>
    public static class PendingStatusDisplay
    {
        public static bool TryGetHarborCharterLine(GameState state, out string line)
        {
            if (!state.HarborCharterPending)
            {
                line = null;
                return false;
            }

            line = "Harbor Charter: next coastal settlement grants +1 VP";
            return true;
        }

        public static bool TryGetEmbargoLine(GameState state, out string line)
        {
            if (!state.AiShopEmbargo.HasValue || state.AiEmbargoDaysLeft <= 0)
            {
                line = null;
                return false;
            }

            var resource = state.AiShopEmbargo.Value;
            string daysLabel = state.AiEmbargoDaysLeft == 1
                ? "1 day"
                : $"{state.AiEmbargoDaysLeft} days";
            line = $"Embargo: AI cannot buy {resource} ({daysLabel} left)";
            return true;
        }

        public static bool TryGetLevelUpPreviewLine(GameState state, int runSeed, out string line)
        {
            if (state.Phase != GamePhase.DayPlayerActions
                || !RunProgression.WillOfferLevelUpAfterThisDay(state))
            {
                line = null;
                return false;
            }

            var choices = RunProgression.PreviewLevelUpChoices(state, runSeed);
            if (choices.Count == 0)
            {
                line = "Ending this day triggers a level-up (no perks left in pool).";
                return true;
            }

            string perkList = string.Join("; ", choices.Select(p => LevelUpLibrary.GetDescription(p)));
            line = $"Level-up after End Day — choose one: {perkList}";
            return true;
        }
    }
}
