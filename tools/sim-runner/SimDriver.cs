using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.SimRunner
{
    /// <summary>
    /// Headless match-driver heuristics (not game rules). Public for dotnet test coverage.
    /// </summary>
    public static class SimDriver
    {
        private static readonly ResourceType[] BuildPriority =
        {
            ResourceType.Wheat,
            ResourceType.Stone,
            ResourceType.Wood,
            ResourceType.Brick,
            ResourceType.Sheep
        };

        private static readonly CardId[] NightCardPriority =
        {
            CardId.YearOfPlenty,
            CardId.RoadBuilder,
            CardId.MasterBuilder,
            CardId.MerchantsLedger,
            CardId.FertileSeason,
            CardId.HarborCharter,
            CardId.Embargo,
            CardId.Monopoly,
            CardId.Knight,
            CardId.BanditRaid,
            CardId.Forecast
        };

        public static void TryBuyAffordableDeals(GameController game)
        {
            if (game.State.ShopDeals == null || game.State.ShopDeals.Count == 0)
                return;

            bool bought;
            do
            {
                bought = false;
                foreach (var deal in game.State.ShopDeals)
                {
                    if (game.BuyShopDeal(deal))
                    {
                        bought = true;
                        break;
                    }
                }
            } while (bought);
        }

        public static bool TryDayBuildOnce(GameController game)
        {
            // Expand network before spending on cities — avoids 2-VP stall on small maps.
            if (TryPlaceFirstValidRoad(game, PlayerId.Human))
                return true;

            if (TryPlaceFirstValidSettlement(game, PlayerId.Human))
                return true;

            foreach (var kvp in game.State.Board.VertexBuildings)
            {
                if (kvp.Value.owner == PlayerId.Human && kvp.Value.type == BuildingType.Settlement)
                {
                    if (game.UpgradeCity(kvp.Key, PlayerId.Human))
                        return true;
                }
            }

            return false;
        }

        public static void TryAllDayBuilds(GameController game)
        {
            while (TryDayBuildOnce(game)) { }
        }

        public static bool TryPlayUsefulNightCard(GameController game)
        {
            if (game.State.PlayerHand.Count == 0)
                return false;

            foreach (var card in NightCardPriority)
            {
                if (!game.State.PlayerHand.Contains(card))
                    continue;

                if (TryPlayNightCard(game, card))
                    return true;
            }

            return false;
        }

        private static bool TryPlayNightCard(GameController game, CardId card)
        {
            switch (card)
            {
                case CardId.RoadBuilder:
                case CardId.MasterBuilder:
                case CardId.HarborCharter:
                case CardId.Forecast:
                    return game.PlayPlayerCard(card);

                case CardId.YearOfPlenty:
                    return game.PlayPlayerCard(card, PickBuildPriorityResource(game));

                case CardId.MerchantsLedger:
                    return game.PlayPlayerCard(card, PickLedgerResource(game));

                case CardId.FertileSeason:
                    return game.PlayPlayerCard(card, PickFertileResource(game));

                case CardId.Embargo:
                    return game.PlayPlayerCard(card, EmbargoTargetSelector.PickTarget(game.State));

                case CardId.Monopoly:
                    var steal = PickAiRichResource(game);
                    return steal.HasValue && game.PlayPlayerCard(card, steal.Value);

                case CardId.Knight:
                    var hex = PickKnightTarget(game);
                    return hex.HasValue && game.PlayPlayerCard(card, null, hex);

                case CardId.BanditRaid:
                    var edge = PickAiRoadToDisable(game);
                    return edge.HasValue && game.PlayPlayerCard(card, null, null, edge);

                default:
                    return false;
            }
        }

        public static ResourceType PickBuildPriorityResource(GameController game)
        {
            var inv = game.State.PlayerInventory;
            return BuildPriority.OrderBy(r => inv[r]).First();
        }

        public static ResourceType PickLedgerResource(GameController game)
        {
            foreach (var r in BuildPriority)
            {
                if (game.State.TomorrowRolls[r] == 0)
                    return r;
            }

            return BuildPriority.First();
        }

        public static ResourceType PickFertileResource(GameController game)
        {
            foreach (var r in BuildPriority)
            {
                if (game.State.TomorrowRolls[r] < 2)
                    return r;
            }

            return BuildPriority.First();
        }

        public static LevelUpPerkId PickBestLevelUpPerk(GameController game)
        {
            return game.State.PendingLevelUpChoices
                .OrderByDescending(p => ScoreLevelUpPerk(p))
                .First();
        }

        private static int ScoreLevelUpPerk(LevelUpPerkId perk) => perk switch
        {
            LevelUpPerkId.FirstCityVp => 10,
            LevelUpPerkId.CheapCities => 8,
            LevelUpPerkId.CheapSettlements => 8,
            LevelUpPerkId.CityProductionBoost => 7,
            LevelUpPerkId.RollInsurance => 6,
            LevelUpPerkId.ThresholdDelay => 6,
            LevelUpPerkId.LongRoadBonus => 5,
            LevelUpPerkId.ExtraCardDraw => 4,
            LevelUpPerkId.ExtraShopDeal => 4,
            LevelUpPerkId.RiskyDealsSafe => 3,
            _ => 1
        };

        public static ResourceType? PickAiRichResource(GameController game)
        {
            var best = game.State.AiInventory.EnumerateNonZero()
                .OrderByDescending(kv => kv.amount)
                .FirstOrDefault();

            if (best.amount <= 0)
                return null;

            return best.type;
        }

        public static HexCoord? PickKnightTarget(GameController game)
        {
            var tile = game.State.Board.VertexBuildings
                .Where(kv => kv.Value.owner == PlayerId.Ai)
                .SelectMany(kv => VertexGraph.GetHexesForVertex(kv.Key))
                .Where(h => game.State.Board.TryGetTile(h, out _))
                .GroupBy(h => h)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            if (tile.Equals(default(HexCoord)))
                return null;

            return tile;
        }

        public static Edge? PickAiRoadToDisable(GameController game)
        {
            var road = game.State.Board.Roads
                .Where(kv => kv.Value == PlayerId.Ai)
                .Select(kv => kv.Key)
                .FirstOrDefault();

            if (road.Equals(default(Edge)))
                return null;

            return road;
        }

        private static bool TryPlaceFirstValidSettlement(GameController game, PlayerId player)
        {
            var spots = game.Placement.GetValidSettlementSpots(
                    game.State.Board, player, game.State.IsSetupPhase)
                .Select(v => (vertex: v, score: ScoreSettlementSpot(game, v)))
                .OrderByDescending(x => x.score)
                .ToList();

            if (spots.Count == 0)
                return false;

            return game.PlaceSettlement(spots[0].vertex, player);
        }

        private static float ScoreSettlementSpot(GameController game, Vertex vertex)
        {
            float score = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!game.State.Board.TryGetTile(hex, out var tile)) continue;
                if (tile.IsDesert || !tile.NumberToken.HasValue) continue;
                score += NumberTokenLibrary.GetPipWeight(tile.NumberToken.Value);
                if (game.State.TodayRolls.TryGetValue(tile.Resource, out int roll))
                    score += roll;
            }

            return score;
        }

        private static bool TryPlaceFirstValidRoad(GameController game, PlayerId player)
        {
            foreach (var edge in game.Placement.GetValidRoadSpots(
                         game.State.Board, player, game.State.IsSetupPhase))
            {
                if (game.PlaceRoad(edge, player))
                    return true;
            }

            return false;
        }
    }
}
