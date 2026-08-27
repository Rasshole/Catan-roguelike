using CatanRoguelike.Core.Leaders;

namespace CatanRoguelike.Core.Shop
{
    /// <summary>
    /// Pure helpers for IMGUI shop deal labels (trade summary, price reason, risky consequence).
    /// </summary>
    public static class ShopDealDisplay
    {
        public const string RiskyRobberConsequence =
            "Robber moves to your best tile when you buy.";

        public static string FormatTradeSummary(ShopDeal deal, int effectiveGive)
        {
            int give = effectiveGive >= 0 ? effectiveGive : deal.GiveAmount;
            string bonus = effectiveGive >= 0 && effectiveGive < deal.GiveAmount ? " (bonus)" : "";
            return $"Give {give} {deal.Give} → Get {deal.ReceiveAmount} {deal.Receive}{bonus}";
        }

        public static string FormatRiskConsequence(GameState state, PlayerId player, ShopDeal deal)
        {
            if (!deal.IsRisky)
                return "";

            if (player == PlayerId.Human && state.HasPerk(LevelUpPerkId.RiskyDealsSafe))
                return "⚠ Risky 2:1 (robber penalty waived)";

            return $"⚠ {RiskyRobberConsequence}";
        }

        public static string FormatShopButtonLabel(
            GameState state,
            PlayerId player,
            ShopDeal deal,
            ShopPriceBreakdown pricing)
        {
            string trade = FormatTradeSummary(deal, pricing.EffectiveGive);
            string reason = ShopDealPricing.FormatShortReason(pricing.Reason);
            string risk = FormatRiskConsequence(state, player, deal);

            if (string.IsNullOrEmpty(risk))
                return $"Buy: {trade} ({reason})";

            return $"Buy: {trade} ({reason})\n{risk}";
        }
    }
}
