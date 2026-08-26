using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Victory;
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

            if (game.State.AiHand.Count == 0) return;

            var card = game.State.AiHand[0];
            var def = CardLibrary.Get(card);
            if (!def.AiCanUse) return;

            ResourceType? target = PickBestResource(game);
            HexCoord? robber = PickRobberTarget(game);
            Edge? road = PickRoadToDisable(game);

            if (game.CardEngine.PlayCard(game.State, PlayerId.Ai, card, target, robber, road))
                _hiddenIntent = $"Played {def.Name}";
        }

        public void ExecuteDayTurn(GameController game)
        {
            _hiddenIntent = "Expand economy";

            TryShopPurchases(game);

            // Try city upgrade
            foreach (var kvp in game.State.Board.VertexBuildings)
            {
                if (kvp.Value.owner == PlayerId.Ai && kvp.Value.type == BuildingType.Settlement)
                {
                    var cost = BalanceConfig.GetCityCost(game.State.Board, PlayerId.Ai);
                    if (game.State.AiInventory.CanAfford(cost))
                    {
                        _hiddenIntent = "Upgrade to city";
                        game.UpgradeCity(kvp.Key, PlayerId.Ai);
                        break;
                    }
                }
            }

            // Try settlement
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

            // Try road toward best expansion or block player
            var roads = game.Placement.GetValidRoadSpots(game.State.Board, PlayerId.Ai, setupPhase: false)
                .Select(e => (e, ScoreRoad(game, e)))
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

        private void TryShopPurchases(GameController game)
        {
            foreach (var deal in game.State.ShopDeals.ToList())
            {
                int cost = game.Shop.GetEffectiveGiveAmount(game.State, PlayerId.Ai, deal);
                var bundle = game.State.AiInventory;
                var need = new ResourceBundle();
                need.Set(deal.Give, cost);
                if (!bundle.CanAfford(need)) continue;

                // Buy if we need the receive resource for a build
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
                score += ResourceValue(tile.Resource);
                if (game.State.TodayRolls.TryGetValue(tile.Resource, out int roll))
                    score += roll;
            }
            return score;
        }

        private float ScoreRoad(GameController game, Edge edge)
        {
            float score = 1f;
            // Prefer roads that block human expansion
            foreach (var v in new[] { edge.A, edge.B })
            {
                var humanSpots = game.Placement
                    .GetValidSettlementSpots(game.State.Board, PlayerId.Human, false)
                    .Count(s => VertexGraph.VertexDistance(s, v) <= 2);
                score += humanSpots * 2f;
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

        private ResourceType PickBestResource(GameController game)
        {
            return game.State.TodayRolls
                .OrderByDescending(kv => kv.Value)
                .First().Key;
        }

        private HexCoord? PickRobberTarget(GameController game)
        {
            var humanTiles = game.State.Board.VertexBuildings
                .Where(kv => kv.Value.owner == PlayerId.Human)
                .SelectMany(kv => VertexGraph.GetHexesForVertex(kv.Key))
                .Where(h => game.State.Board.TryGetTile(h, out _))
                .GroupBy(h => h)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
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
