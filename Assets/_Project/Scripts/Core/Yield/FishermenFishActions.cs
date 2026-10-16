using CatanRoguelike.Core.Cards;

namespace CatanRoguelike.Core.Yield
{
    public static class FishermenFishActions
    {
        public static bool TrySpend(GameState state, PlayerId player, FishermenSpendAction action)
        {
            if (!FishermenRules.IsEnabled(state))
                return false;

            int cost = FishermenRules.GetFishCost(action);
            if (player == PlayerId.Human)
            {
                if (state.PlayerFish < cost)
                    return false;
                state.PlayerFish -= cost;
            }
            else
            {
                if (state.AiFish < cost)
                    return false;
                state.AiFish -= cost;
            }

            switch (action)
            {
                case FishermenSpendAction.MoveRobberOffBoard:
                    state.Board.ClearRobber();
                    break;
                case FishermenSpendAction.StealFromOpponent:
                    StealOneResource(state, player);
                    break;
                case FishermenSpendAction.FreeRoad:
                    if (player == PlayerId.Human)
                        state.FreeRoadCharges++;
                    break;
                case FishermenSpendAction.DrawResource:
                    GrantRandomBankResource(state, player);
                    break;
                case FishermenSpendAction.DrawDevelopmentCard:
                    if (player == PlayerId.Human && !state.PlayerHand.Contains(CardId.Knight))
                        state.PlayerHand.Add(CardId.Knight);
                    break;
            }

            return true;
        }

        private static void StealOneResource(GameState state, PlayerId player)
        {
            var victim = player == PlayerId.Human ? PlayerId.Ai : PlayerId.Human;
            var inv = state.GetInventory(victim);
            foreach (var type in new[] { ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat, ResourceType.Sheep, ResourceType.Stone })
            {
                if (inv[type] <= 0)
                    continue;
                inv.Add(type, -1);
                state.SetInventory(victim, inv);
                var thief = state.GetInventory(player);
                thief.Add(type, 1);
                state.SetInventory(player, thief);
                return;
            }
        }

        private static void GrantRandomBankResource(GameState state, PlayerId player)
        {
            foreach (var type in new[] { ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat, ResourceType.Sheep, ResourceType.Stone })
            {
                if (!state.Bank.TryWithdraw(type, 1))
                    continue;
                var inv = state.GetInventory(player);
                inv.Add(type, 1);
                state.SetInventory(player, inv);
                return;
            }
        }
    }
}
