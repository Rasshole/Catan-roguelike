using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Victory;
using CatanRoguelike.Core.Yield;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core
{
    /// <summary>Heuristic AI with hidden intents (not exposed to UI).</summary>
    public sealed class AiController
    {
        private readonly Random _random;
        private string _hiddenIntent = "";

        public AiController(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public void PlaceSetupSettlement(GameController game)
        {
            var spots = game.Placement.GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: true).ToList();
            if (spots.Count == 0) return;

            var best = spots
                .Select(v => (vertex: v, score: ScoreSettlementSpot(game, v)))
                .OrderByDescending(x => x.score)
                .First();

            _hiddenIntent = $"Secure {DescribeVertex(game, best.vertex)}";
            game.PlaceSettlement(best.vertex, PlayerId.Ai);
        }

        public void PlaceSetupRoad(GameController game)
        {
            var roads = game.Placement.GetValidRoadSpots(game.State.Board, PlayerId.Ai, setupPhase: true).ToList();
            if (roads.Count == 0) return;

            var pick = roads[_random.Next(roads.Count)];
            _hiddenIntent = "Extend setup network";
            game.PlaceRoad(pick, PlayerId.Ai);
        }

        public void ExecuteNightPlan(GameController game)
        {
            _hiddenIntent = "Hold position";

            int act = ActProgression.GetAct(game.State.Board.DayNumber);
            int maxPlays = ActProgression.GetAiNightCardPlays(act);

            for (int play = 0; play < maxPlays; play++)
            {
                if (game.State.AiHand.Count == 0)
                    break;

                var card = PickBestAiCard(game, act);
                var def = CardLibrary.Get(card);
                if (!def.AiCanUse)
                    break;

                ResourceType? target = card == CardId.Embargo
                    ? EmbargoTargetSelector.PickTarget(game.State)
                    : PickBestResource(game);
                HexCoord? robber = PickRobberTarget(game);
                Edge? road = PickRoadToDisable(game);

                if (!game.CardEngine.PlayCard(game.State, PlayerId.Ai, card, target, robber, road))
                    break;

                _hiddenIntent = card == CardId.Embargo
                    ? $"Embargo {target}"
                    : $"Played {def.Name}";
            }
        }

        private static float ScoreKnightForLargestArmy(GameState state)
        {
            int ai = state.AiKnightsPlayed;
            int human = state.PlayerKnightsPlayed;
            float bonus = 0f;

            if (ai == BalanceConfig.LargestArmyThreshold - 1)
                bonus += 3f;

            if (state.LargestArmyOwner == PlayerId.Human
                && ai + 1 >= BalanceConfig.LargestArmyThreshold
                && ai + 1 > human)
            {
                bonus += 4f;
            }

            if (state.LargestArmyOwner == null
                && ai + 1 >= BalanceConfig.LargestArmyThreshold
                && ai + 1 > human)
            {
                bonus += 3f;
            }

            if (state.LargestArmyOwner == PlayerId.Ai)
                bonus += 1f;

            return bonus;
        }

        public void ExecuteDayTurn(GameController game)
        {
            _hiddenIntent = "Expand economy";

            int act = ActProgression.GetAct(game.State.Board.DayNumber);
            TryShopPurchases(game);

            // Act 2+: prioritize all affordable city upgrades (VP pressure)
            bool upgradedCity = false;
            foreach (var kvp in game.State.Board.VertexBuildings.ToList())
            {
                if (kvp.Value.owner != PlayerId.Ai || kvp.Value.type != BuildingType.Settlement)
                    continue;

                var cost = BalanceConfig.GetCityCost(game.State.Board, PlayerId.Ai);
                if (!game.State.AiInventory.CanAfford(cost))
                    continue;

                _hiddenIntent = "Upgrade to city";
                game.UpgradeCity(kvp.Key, PlayerId.Ai);
                upgradedCity = true;
                if (act < 2)
                    break;
            }

            if (!upgradedCity)
            {
                // Try settlement on best spot
                var settlementSpots = game.Placement
                    .GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: false)
                    .Select(v => (v, ScoreSettlementSpot(game, v)))
                    .OrderByDescending(x => x.Item2)
                    .ToList();

                if (settlementSpots.Count > 0)
                {
                    var cost = BalanceConfig.GetSettlementCost(game.State.Board, PlayerId.Ai);
                    if (game.State.AiInventory.CanAfford(cost))
                    {
                        _hiddenIntent = "Build settlement";
                        game.PlaceSettlement(settlementSpots[0].v, PlayerId.Ai);
                    }
                }
            }

            // Try road toward best expansion or block player
            var roads = game.Placement.GetValidRoadSpots(game.State.Board, PlayerId.Ai, setupPhase: false)
                .Select(e => (e, ScoreRoad(game, e, act)))
                .OrderByDescending(x => x.Item2)
                .ToList();

            if (roads.Count > 0)
            {
                var cost = BalanceConfig.GetRoadCost(game.State.Board, PlayerId.Ai);
                if (game.State.AiInventory.CanAfford(cost))
                {
                    _hiddenIntent = roads[0].Item2 > 5 ? "Block player route" : "Extend route";
                    game.PlaceRoad(roads[0].e, PlayerId.Ai);
                }
            }

            // Move robber if on good target
            var robberTarget = PickRobberTarget(game);
            if (robberTarget.HasValue)
            {
                game.MoveRobber(robberTarget.Value, PlayerId.Ai, steal: true);
                _hiddenIntent = "Move robber";
            }
        }

        private CardId PickBestAiCard(GameController game, int act)
        {
            var scored = game.State.AiHand
                .Where(c => CardLibrary.Get(c).AiCanUse)
                .Select(c => (card: c, score: ScoreAiCard(game, c, act)))
                .OrderByDescending(x => x.score)
                .ToList();

            return scored.Count > 0 ? scored[0].card : game.State.AiHand[0];
        }

        private float ScoreAiCard(GameController game, CardId card, int act)
        {
            float score = CardLibrary.Get(card).AiWeight;

            switch (card)
            {
                case CardId.Knight:
                    score += PickRobberTarget(game).HasValue ? 3f : 0f;
                    score += ScoreKnightForLargestArmy(game.State);
                    break;
                case CardId.Monopoly:
                    var humanInv = game.State.PlayerInventory;
                    score += humanInv.Wood + humanInv.Brick + humanInv.Wheat
                        + humanInv.Sheep + humanInv.Stone;
                    break;
                case CardId.Embargo:
                    score += 2f;
                    break;
                case CardId.BanditRaid:
                    score += game.State.Board.Roads.Count(kv => kv.Value == PlayerId.Human) * 1.5f;
                    break;
                case CardId.Drought:
                    score += game.State.TodayRolls.Values.DefaultIfEmpty(0).Max();
                    break;
                case CardId.FertileSeason:
                    score += act >= 3 ? 1.5f : 0.5f;
                    break;
            }

            return score;
        }

        private void TryShopPurchases(GameController game)
        {
            foreach (var deal in game.State.ShopDeals.ToList())
            {
                if (game.State.AiShopEmbargo.HasValue
                    && game.State.AiShopEmbargo.Value == deal.Give)
                {
                    _hiddenIntent = $"Skip shop ({deal.Give} embargoed)";
                    continue;
                }

                int cost = game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Ai, deal);
                var bundle = game.State.AiInventory;
                var need = new ResourceBundle();
                need.Set(deal.Give, cost);
                if (!bundle.CanAfford(need)) continue;

                if (deal.Receive == ResourceType.Wheat || deal.Receive == ResourceType.Stone
                    || deal.Receive == ResourceType.Sheep || deal.Receive == ResourceType.Wood
                    || deal.Receive == ResourceType.Brick)
                {
                    if (game.Shop.TryPurchase(game.State, PlayerId.Ai, deal))
                    {
                        _hiddenIntent = $"Bought shop deal ({deal.Receive})";
                        break;
                    }
                }
            }
        }

        private float ScoreSettlementSpot(GameController game, Vertex vertex)
        {
            float score = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!game.State.Board.TryGetTile(hex, out var tile)) continue;
                if (tile.IsDesert || !tile.NumberToken.HasValue) continue;
                score += ResourceValue(tile.Resource);
                score += NumberTokenLibrary.GetPipWeight(tile.NumberToken.Value);
                if (game.State.TodayRolls.TryGetValue(tile.Resource, out int roll))
                    score += roll;
            }
            return score;
        }

        private float ScoreRoad(GameController game, Edge edge, int act)
        {
            float score = 1f;
            float blockWeight = act >= 2 ? 3f : 2f;
            foreach (var v in new[] { edge.A, edge.B })
            {
                var humanSpots = game.Placement
                    .GetValidSettlementSpots(game.State.Board, PlayerId.Human, false)
                    .Count(s => VertexGraph.VertexDistance(s, v) <= 2);
                score += humanSpots * blockWeight;
            }
            return score;
        }

        private static int ResourceValue(ResourceType r) => r switch
        {
            ResourceType.Wheat => 3,
            ResourceType.Stone => 3,
            ResourceType.Wood => 2,
            ResourceType.Brick => 2,
            ResourceType.Sheep => 2,
            _ => 1
        };

        private static ResourceType PickBestResource(GameController game)
        {
            if (game.State.TodayRolls.Count > 0)
            {
                return game.State.TodayRolls
                    .OrderByDescending(kv => kv.Value)
                    .First().Key;
            }

            return ResourceType.Wheat;
        }

        private HexCoord? PickRobberTarget(GameController game)
        {
            var humanTiles = game.State.Board.VertexBuildings
                .Where(kv => kv.Value.owner == PlayerId.Human)
                .SelectMany(kv => VertexGraph.GetHexesForVertex(kv.Key))
                .Where(h => game.State.Board.TryGetTile(h, out _))
                .GroupBy(h => h)
                .Select(g =>
                {
                    game.State.Board.TryGetTile(g.Key, out var tile);
                    int pip = tile.NumberToken.HasValue
                        ? NumberTokenLibrary.GetPipWeight(tile.NumberToken.Value)
                        : 1;
                    return (coord: g.Key, score: g.Count() * pip);
                })
                .OrderByDescending(x => x.score)
                .Select(x => x.coord)
                .FirstOrDefault();

            if (humanTiles.Equals(default(HexCoord))) return null;
            return humanTiles;
        }

        private Edge? PickRoadToDisable(GameController game)
        {
            var humanRoads = game.State.Board.Roads
                .Where(kv => kv.Value == PlayerId.Human)
                .Select(kv => kv.Key)
                .ToList();

            if (humanRoads.Count == 0) return null;
            return humanRoads[_random.Next(humanRoads.Count)];
        }

        private static string DescribeVertex(GameController game, Vertex v)
        {
            foreach (var hex in VertexGraph.GetHexesForVertex(v))
            {
                if (game.State.Board.TryGetTile(hex, out var tile))
                    return tile.Resource.ToString();
            }
            return "position";
        }
    }
}
