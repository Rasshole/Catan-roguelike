namespace CatanRoguelike.Core
{
    /// <summary>
    /// Pure helpers for IMGUI status lines about pending card effects (Harbor Charter, Embargo).
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
    }
}
