using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core
{
    public static class RobberSteal
    {
        public static IReadOnlyList<PlayerId> GetVictimsOnHex(GameState state, HexCoord hex, PlayerId stealer)
        {
            var victims = new HashSet<PlayerId>();
            foreach (var kvp in state.Board.VertexBuildings)
            {
                if (kvp.Value.owner == stealer) continue;
                foreach (var h in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!h.Equals(hex)) continue;
                    victims.Add(kvp.Value.owner);
                    break;
                }
            }
            return victims.ToList();
        }

        /// <summary>
        /// Steals up to <paramref name="count"/> random resources from players
        /// with buildings adjacent to <paramref name="hex"/>.
        /// Returns how many resources were actually stolen.
        /// </summary>
        public static int StealFromHex(GameState state, HexCoord hex, PlayerId stealer, Random random, int count = 1)
        {
            int stolen = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryStealOnce(state, hex, stealer, random))
                    break;
                stolen++;
            }
            return stolen;
        }

        private static bool TryStealOnce(GameState state, HexCoord hex, PlayerId stealer, Random random)
        {
            var victims = GetVictimsOnHex(state, hex, stealer)
                .Where(v => state.GetInventory(v).Total > 0)
                .ToList();
            if (victims.Count == 0) return false;

            var victim = victims[random.Next(victims.Count)];
            var victimInv = state.GetInventory(victim);
            var available = victimInv.EnumerateNonZero().ToList();
            if (available.Count == 0) return false;

            var pick = available[random.Next(available.Count)];
            victimInv.Add(pick.type, -1);
            var stealerInv = state.GetInventory(stealer);
            stealerInv.Add(pick.type, 1);
            state.SetInventory(victim, victimInv);
            state.SetInventory(stealer, stealerInv);
            return true;
        }
    }
}
