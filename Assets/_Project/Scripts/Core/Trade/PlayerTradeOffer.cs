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
        /// <summary>v139: wildcards the human gives ("N of any resource you choose": the AI picks).</summary>
        public readonly int GiveAny;
        /// <summary>v139: wildcards the human asks for ("N of anything": the human picks when trading).</summary>
        public readonly int ReceiveAny;

        public PlayerTradeOffer(ResourceBundle give, ResourceBundle receive, int giveAny = 0, int receiveAny = 0)
        {
            Give = give;
            Receive = receive;
            GiveAny = giveAny;
            ReceiveAny = receiveAny;
        }
    }

    public readonly struct AiTradeResponse
    {
        public readonly PlayerId Ai;
        public readonly bool Accept;
        /// <summary>v139: what the AI takes for the human's give wildcards.</summary>
        public readonly ResourceBundle Choice;

        public AiTradeResponse(PlayerId ai, bool accept, ResourceBundle choice = default)
        {
            Ai = ai;
            Accept = accept;
            Choice = choice;
        }
    }

    public static class PlayerTradeResolver
    {
        public static bool IsAffordable(GameState state, PlayerId player, ResourceBundle cost) =>
            state.GetInventory(player).CanAfford(cost);

        /// <summary>v139: the offer's shape is legal (cards on both sides, wildcards on one side, backed).</summary>
        public static bool IsValidOffer(GameState state, PlayerTradeOffer offer)
        {
            if (!TradeWildcards.IsValid(offer.Give) || !TradeWildcards.IsValid(offer.Receive))
                return false;
            if (offer.GiveAny < 0 || offer.ReceiveAny < 0 || offer.GiveAny > TradeWildcards.MaxWildcards
                || offer.ReceiveAny > TradeWildcards.MaxWildcards || offer.GiveAny > 0 && offer.ReceiveAny > 0)
                return false;
            if (offer.Give.Total + offer.GiveAny == 0 || offer.Receive.Total + offer.ReceiveAny == 0)
                return false;
            foreach (var r in TradeWildcards.Resources)
                if (offer.Give[r] > 0 && offer.Receive[r] > 0)
                    return false;
            if (!IsAffordable(state, PlayerId.Human, offer.Give))
                return false;
            return offer.GiveAny == 0 || HumanWildPool(state, offer).Total >= offer.GiveAny;
        }

        /// <summary>v139: what the human's give wildcards may become (never a resource the human asks for).</summary>
        public static ResourceBundle HumanWildPool(GameState state, PlayerTradeOffer offer) =>
            TradeWildcards.Pool(state.PlayerInventory, offer.Give, offer.Receive);

        /// <summary>v139: what the human may take for the receive wildcards from the AI's hand.</summary>
        public static ResourceBundle AiWildPool(GameState state, PlayerTradeOffer offer, ResourceBundle aiChoice) =>
            TradeWildcards.Pool(state.AiInventory, offer.Receive, offer.Give, aiChoice);

        public static IReadOnlyList<AiTradeResponse> EvaluateAiResponses(
            GameState state,
            PlayerTradeOffer offer)
        {
            var responses = new List<AiTradeResponse>();
            if (!IsValidOffer(state, offer))
                return responses;

            var choice = offer.GiveAny > 0
                ? TradeWildcards.PickForNeed(HumanWildPool(state, offer), offer.GiveAny, state.AiInventory)
                : ResourceBundle.Zero;
            bool aiCanPay = IsAffordable(state, PlayerId.Ai, offer.Receive)
                            && (offer.ReceiveAny == 0 || AiWildPool(state, offer, choice).Total >= offer.ReceiveAny);
            bool accept = aiCanPay && offer.Give.Total + offer.GiveAny > 0 && offer.Receive.Total + offer.ReceiveAny > 0;
            responses.Add(new AiTradeResponse(PlayerId.Ai, accept, accept ? choice : ResourceBundle.Zero));
            return responses;
        }

        public static bool TryExecute(GameState state, PlayerId acceptor, PlayerTradeOffer offer) =>
            TryExecute(state, acceptor, offer, ResourceBundle.Zero, ResourceBundle.Zero);

        /// <summary>
        /// v139: execute with wildcards resolved: <paramref name="aiChoice"/> = what the AI takes for the human's
        /// give wildcards, <paramref name="humanChoice"/> = what the human takes for the receive wildcards.
        /// </summary>
        public static bool TryExecute(GameState state, PlayerId acceptor, PlayerTradeOffer offer, ResourceBundle aiChoice,
            ResourceBundle humanChoice)
        {
            if (acceptor != PlayerId.Ai)
                return false;
            if (!IsValidOffer(state, offer) || !TradeWildcards.IsValid(aiChoice) || !TradeWildcards.IsValid(humanChoice))
                return false;
            if (offer.GiveAny > 0 ? !TradeWildcards.Fits(aiChoice, HumanWildPool(state, offer), offer.GiveAny) : aiChoice.Total != 0)
                return false;
            if (offer.ReceiveAny > 0
                    ? !TradeWildcards.Fits(humanChoice, AiWildPool(state, offer, aiChoice), offer.ReceiveAny)
                    : humanChoice.Total != 0)
                return false;
            var give = offer.Give;
            give.Add(aiChoice);
            var receive = offer.Receive;
            receive.Add(humanChoice);
            if (!IsAffordable(state, PlayerId.Human, give))
                return false;
            if (!IsAffordable(state, PlayerId.Ai, receive))
                return false;

            var human = state.PlayerInventory;
            human.Pay(give);
            human.Add(receive);
            state.PlayerInventory = human;

            var ai = state.AiInventory;
            ai.Pay(receive);
            ai.Add(give);
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
