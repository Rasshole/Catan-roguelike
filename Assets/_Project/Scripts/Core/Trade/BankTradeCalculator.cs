using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;

namespace CatanRoguelike.Core.Trade
{
    public static class BankTradeCalculator
    {
        public const int BankRate = 4;
        public const int GenericPortRate = 3;
        public const int SpecificPortRate = 2;

        public static int GetGiveRateForResource(GameState state, PlayerId player, ResourceType give)
        {
            var blocked = state.EventBlockedPortVertex;
            if (PortAccess.HasSpecificPort(state.Board, player, give, state.Ports, blocked))
                return SpecificPortRate;
            if (PortAccess.HasGenericPort(state.Board, player, state.Ports, blocked))
                return GenericPortRate;
            return CivilizationRules.BaseBankRate(state, player, BankRate);
        }

        public static bool CanTrade(
            GameState state,
            PlayerId player,
            ResourceType give,
            ResourceType receive,
            int receiveAmount)
        {
            if (receiveAmount <= 0 || give == receive)
                return false;

            int rate = GetGiveRateForResource(state, player, give);
            int pay = rate * receiveAmount;
            var inv = player == PlayerId.Human ? state.PlayerInventory : state.AiInventory;
            return inv[give] >= pay;
        }

        public static bool TryExecute(
            GameState state,
            PlayerId player,
            ResourceType give,
            ResourceType receive,
            int receiveAmount)
        {
            if (!CanTrade(state, player, give, receive, receiveAmount))
                return false;

            int rate = GetGiveRateForResource(state, player, give);
            int pay = rate * receiveAmount;

            if (player == PlayerId.Human)
            {
                var inv = state.PlayerInventory;
                inv.Set(give, inv[give] - pay);
                inv.Set(receive, inv[receive] + receiveAmount);
                state.PlayerInventory = inv;
            }
            else
            {
                var inv = state.AiInventory;
                inv.Set(give, inv[give] - pay);
                inv.Set(receive, inv[receive] + receiveAmount);
                state.AiInventory = inv;
            }

            return true;
        }
    }
}
