using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Trade
{
    public readonly struct PlayerTradeOffer
    {
        public readonly ResourceBundle Give;
        public readonly ResourceBundle Receive;

        public PlayerTradeOffer(ResourceBundle give, ResourceBundle receive)
        {
            Give = give;
            Receive = receive;
        }
    }

    public readonly struct AiTradeResponse
    {
        public readonly PlayerId Ai;
        public readonly bool Accept;

        public AiTradeResponse(PlayerId ai, bool accept)
        {
            Ai = ai;
            Accept = accept;
        }
    }

    public static class PlayerTradeResolver
    {
        public static bool IsAffordable(GameState state, PlayerId player, ResourceBundle cost) =>
            state.GetInventory(player).CanAfford(cost);

        public static IReadOnlyList<AiTradeResponse> EvaluateAiResponses(
            GameState state,
            PlayerTradeOffer offer)
        {
            var responses = new List<AiTradeResponse>();
            if (!IsAffordable(state, PlayerId.Human, offer.Give))
                return responses;

            bool aiCanPay = IsAffordable(state, PlayerId.Ai, offer.Receive);
            bool aiWantsGive = offer.Give.Total > 0;
            bool accept = aiCanPay && aiWantsGive && offer.Receive.Total > 0;
            responses.Add(new AiTradeResponse(PlayerId.Ai, accept));
            return responses;
        }

        public static bool TryExecute(GameState state, PlayerId acceptor, PlayerTradeOffer offer)
        {
            if (acceptor != PlayerId.Ai)
                return false;
            if (!IsAffordable(state, PlayerId.Human, offer.Give))
                return false;
            if (!IsAffordable(state, PlayerId.Ai, offer.Receive))
                return false;

            var human = state.PlayerInventory;
            human.Pay(offer.Give);
            human.Add(offer.Receive);
            state.PlayerInventory = human;

            var ai = state.AiInventory;
            ai.Pay(offer.Receive);
            ai.Add(offer.Give);
            state.AiInventory = ai;
            return true;
        }
    }

    public readonly struct AiIncomingTradeOffer
    {
        public readonly ResourceBundle Give;
        public readonly ResourceBundle Receive;

        public AiIncomingTradeOffer(ResourceBundle give, ResourceBundle receive)
        {
            Give = give;
            Receive = receive;
        }
    }

    public static class AiTradeOfferGenerator
    {
        public static bool TryCreateOffer(GameState state, Random random, out AiIncomingTradeOffer offer)
        {
            offer = default;
            if (state.GetInventory(PlayerId.Ai).Total < 2)
                return false;
            if (random.NextDouble() > 0.35)
                return false;

            var aiTypes = new List<ResourceType>();
            foreach (var pair in state.AiInventory.EnumerateNonZero())
                aiTypes.Add(pair.type);
            if (aiTypes.Count == 0)
                return false;

            var giveType = aiTypes[random.Next(aiTypes.Count)];
            var receiveType = (ResourceType)random.Next(0, 5);
            if (receiveType == giveType)
                receiveType = ResourceType.Wheat;

            var give = ResourceBundle.Zero;
            give.Set(giveType, 1);
            var receive = ResourceBundle.Zero;
            receive.Set(receiveType, 1);
            offer = new AiIncomingTradeOffer(give, receive);
            return true;
        }
    }
}
