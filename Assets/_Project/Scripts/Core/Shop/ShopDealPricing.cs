using System;
using System.Linq;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Shop
{
    public enum ShopPriceReasonKind
    {
        Base,
        Port2To1,
        Port3To1,
        Leader,
        Event,
        PerkPortDiscount
    }

    public readonly struct ShopPriceBreakdown
    {
        public int EffectiveGive { get; }
        public ShopPriceReasonKind Reason { get; }

        public ShopPriceBreakdown(int effectiveGive, ShopPriceReasonKind reason)
        {
            EffectiveGive = effectiveGive;
            Reason = reason;
        }
    }

    /// <summary>
    /// Classifies why a shop deal costs its effective give amount (port / leader / event / base).
    /// Mirrors <see cref="ShopGenerator.GetEffectiveGiveAmount"/> without side effects.
    /// </summary>
    public static class ShopDealPricing
    {
        public static ShopPriceBreakdown Analyze(GameState state, PlayerId player, ShopDeal deal)
        {
            int modifierGive = ModifierService.GetShopGiveAmount(state, player, deal.GiveAmount, deal.Give);
            int portGive = PortAccess.GetEffectiveGiveAmount(
                state.Board, player, deal, state.Ports, state.EventBlockedPortVertex);
            int effective = Math.Min(modifierGive, portGive);
            var reason = ClassifyReason(state, player, deal, modifierGive, portGive, effective);
            return new ShopPriceBreakdown(effective, reason);
        }

        public static string FormatShortReason(ShopPriceReasonKind reason) => reason switch
        {
            ShopPriceReasonKind.Port2To1 => "port 2:1",
            ShopPriceReasonKind.Port3To1 => "port 3:1",
            ShopPriceReasonKind.Leader => "leader",
            ShopPriceReasonKind.Event => "event",
            ShopPriceReasonKind.PerkPortDiscount => "perk",
            _ => "base"
        };

        private static ShopPriceReasonKind ClassifyReason(
            GameState state,
            PlayerId player,
            ShopDeal deal,
            int modifierGive,
            int portGive,
            int effective)
        {
            if (effective >= deal.GiveAmount)
                return ShopPriceReasonKind.Base;

            bool hasSpecific = PortAccess.HasSpecificPort(
                state.Board, player, deal.Give, state.Ports, state.EventBlockedPortVertex);
            bool hasGeneric = PortAccess.HasGenericPort(
                state.Board, player, state.Ports, state.EventBlockedPortVertex);

            if (effective == portGive && portGive < modifierGive)
            {
                if (hasSpecific) return ShopPriceReasonKind.Port2To1;
                if (hasGeneric) return ShopPriceReasonKind.Port3To1;
            }

            if (effective == portGive && effective == modifierGive)
            {
                if (hasSpecific) return ShopPriceReasonKind.Port2To1;
                if (hasGeneric && portGive <= 3) return ShopPriceReasonKind.Port3To1;
            }

            if (effective == modifierGive)
                return ClassifyModifierReason(state, player, deal);

            return ShopPriceReasonKind.Base;
        }

        private static ShopPriceReasonKind ClassifyModifierReason(GameState state, PlayerId player, ShopDeal deal)
        {
            int afterEvent = deal.GiveAmount - state.EventShopBonus;
            bool eventActive = state.EventShopBonus > 0 && afterEvent < deal.GiveAmount;

            bool merchantActive = player == PlayerId.Human && state.Leader == LeaderId.Merchant;
            int afterMerchant = merchantActive ? Math.Max(2, afterEvent - 1) : afterEvent;

            bool perkActive = player == PlayerId.Human
                && state.HasPerk(LevelUpPerkId.PortDiscount)
                && state.Ports.Any(p => PortAccess.PlayerControlsVertex(state.Board, player, p.Vertex));
            int afterPerk = perkActive ? Math.Max(2, afterMerchant - 1) : afterMerchant;

            if (perkActive && afterPerk < afterMerchant)
                return ShopPriceReasonKind.PerkPortDiscount;
            if (merchantActive && afterMerchant < afterEvent)
                return ShopPriceReasonKind.Leader;
            if (eventActive)
                return ShopPriceReasonKind.Event;

            return ShopPriceReasonKind.Base;
        }
    }
}
