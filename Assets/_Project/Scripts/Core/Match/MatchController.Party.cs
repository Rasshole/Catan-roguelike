using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v136 party rules (pass-and-play step 4): civ pick before placement, secret quest + trait pick after placement
    /// (last placer first), civ/trait rule modifiers and an event card each round. Only active when
    /// <see cref="MatchConfig.PartyRules"/> is on; every random draw uses <see cref="MatchState.PartyRng"/>.
    /// </summary>
    public sealed partial class MatchController
    {
        private bool _questsRevealed;

        // ---------- civ pick ----------

        private void BeginCivPick()
        {
            State.CivDeck.AddRange(Shuffled(CivilizationLibrary.Pool.Select(c => c.Id)));
            State.QuestDeck.AddRange(Shuffled(MatchPartyLibrary.Quests.Select(q => q.Id)));
            State.TraitDeck.AddRange(Shuffled(MatchPartyLibrary.Traits.Select(t => t.Id)));
            State.EventDeck.AddRange(Shuffled(MatchPartyLibrary.Events.Select(e => e.Id)));
            State.PickOrder.Clear();
            for (int i = 0; i < State.Seats.Count; i++)
                State.PickOrder.Add(i);
            State.PickStep = 0;
            State.Phase = MatchPhase.CivPick;
            State.CurrentSeat = State.PickOrder[0];
            DealCivOffer(State.Current);
            AppendLog("Each player picks a civilization in secret.");
        }

        private List<T> Shuffled<T>(IEnumerable<T> items)
        {
            var list = items.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = State.PartyRng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list;
        }

        private void DealCivOffer(MatchSeatState seat)
        {
            seat.CivOffer.Clear();
            int n = System.Math.Min(MatchPartyLibrary.CivOfferSize, State.CivDeck.Count);
            seat.CivOffer.AddRange(State.CivDeck.Take(n));
            State.CivDeck.RemoveRange(0, n);
        }

        private MatchCommandResult PickCivilization(MatchCommand c)
        {
            if (State.Phase != MatchPhase.CivPick)
                return MatchCommandResult.Rejected("Not picking civilizations now.");
            var seat = State.Seats[c.Seat];
            var civ = (CivilizationId)c.Pick;
            if (!seat.CivOffer.Contains(civ))
                return MatchCommandResult.Rejected("Pick one of the offered civilizations.");
            seat.Civilization = civ;
            State.CivDeck.AddRange(seat.CivOffer.Where(o => o != civ));
            seat.CivOffer.Clear();
            if (civ == CivilizationId.ShepherdKingdom)
                Give(seat, ResourceType.Sheep, 2);

            State.PickStep++;
            if (State.PickStep >= State.PickOrder.Count)
            {
                AppendLog("Civilizations: " + string.Join(", ", State.Seats.Select(s =>
                    $"{s.Name} – {CivilizationLibrary.Get(s.Civilization)?.Name}")) + ".");
                State.Phase = MatchPhase.SetupSettlement;
                State.CurrentSeat = State.SetupOrder[0];
                AppendLog($"{State.Current.Name} places a settlement.");
            }
            else
            {
                State.CurrentSeat = State.PickOrder[State.PickStep];
                DealCivOffer(State.Current);
            }

            return MatchCommandResult.Accepted;
        }

        // ---------- quest + trait pick ----------

        private void BeginQuestPick()
        {
            // Reverse placement order: the last seat of the first placement round picks first (compensation).
            State.PickOrder.Clear();
            for (int i = State.Seats.Count - 1; i >= 0; i--)
                State.PickOrder.Add(i);
            State.PickStep = 0;
            State.Phase = MatchPhase.QuestPick;
            State.CurrentSeat = State.PickOrder[0];
            DealQuestOffer(State.Current);
            AppendLog("Setup complete. Secret quests are dealt – last placer picks first.");
        }

        private void DealQuestOffer(MatchSeatState seat)
        {
            int idx = seat.Index;
            var deck = State.QuestDeck;
            var defs = deck.Select(MatchPartyLibrary.Quest).Where(q => q != null && !q.IsComplete(this, idx)).ToList();
            var plausible = defs.Where(q => q.Plausible(this, idx)).ToList();
            var chosen = new List<MatchQuestDefinition>();

            void Take(IEnumerable<MatchQuestDefinition> from)
            {
                var q = from.FirstOrDefault(x => !chosen.Contains(x));
                if (q != null && chosen.Count < MatchPartyLibrary.QuestOfferSize)
                    chosen.Add(q);
            }

            // A mix: one easier 1-VP quest, one harder 2-VP quest, then the next fitting one.
            Take(plausible.Where(q => q.Points == 1));
            Take(plausible.Where(q => q.Points >= 2));
            Take(plausible);
            while (chosen.Count < MatchPartyLibrary.QuestOfferSize && defs.Any(q => !chosen.Contains(q)))
                Take(defs);

            seat.QuestOffer.Clear();
            seat.QuestOffer.AddRange(chosen.OrderBy(q => q.Points).ThenBy(q => deck.IndexOf(q.Id)).Select(q => q.Id));
            foreach (var q in seat.QuestOffer)
                deck.Remove(q);

            seat.TraitOffer.Clear();
            int n = System.Math.Min(MatchPartyLibrary.TraitOfferSize, State.TraitDeck.Count);
            seat.TraitOffer.AddRange(State.TraitDeck.Take(n));
            State.TraitDeck.RemoveRange(0, n);
        }

        private MatchCommandResult PickQuest(MatchCommand c)
        {
            if (State.Phase != MatchPhase.QuestPick)
                return MatchCommandResult.Rejected("Not picking quests now.");
            var seat = State.Seats[c.Seat];
            var quest = (MatchQuestId)c.Pick;
            var trait = (MatchTraitId)c.Pick2;
            if (!seat.QuestOffer.Contains(quest))
                return MatchCommandResult.Rejected("Pick one of the offered quests.");
            if (!seat.TraitOffer.Contains(trait))
                return MatchCommandResult.Rejected("Pick one of the offered traits.");
            seat.Quest = quest;
            seat.Trait = trait;
            State.QuestDeck.AddRange(seat.QuestOffer.Where(q => q != quest));
            State.TraitDeck.AddRange(seat.TraitOffer.Where(t => t != trait));
            seat.QuestOffer.Clear();
            seat.TraitOffer.Clear();
            if (trait == MatchTraitId.HeadStart)
            {
                Give(seat, ResourceType.Wood, 1);
                Give(seat, ResourceType.Brick, 1);
            }

            AppendLog($"{seat.Name} chose a secret quest.");
            State.PickStep++;
            if (State.PickStep >= State.PickOrder.Count)
            {
                State.CurrentSeat = 0;
                State.TurnNumber = 1;
                State.Phase = MatchPhase.Roll;
                AppendLog($"Quests chosen. {State.Current.Name} rolls first.");
            }
            else
            {
                State.CurrentSeat = State.PickOrder[State.PickStep];
                DealQuestOffer(State.Current);
            }

            return MatchCommandResult.Accepted;
        }

        // ---------- civ / trait triggers ----------

        private void OnSevenParty()
        {
            foreach (var seat in State.Seats)
            {
                switch (seat.Civilization)
                {
                    case CivilizationId.MerchantRepublic when seat.CardCount > 0:
                        TakeRandom(seat, null);
                        break;
                    case CivilizationId.Brickwrights:
                        Give(seat, ResourceType.Brick, 1);
                        break;
                    case CivilizationId.NorthernRaiders:
                    {
                        var target = State.Seats.Where(o => o.Index != seat.Index && o.CardCount > 0)
                            .OrderByDescending(o => o.CardCount).ThenBy(o => o.Index).FirstOrDefault();
                        if (target != null)
                        {
                            TakeRandom(target, seat);
                            AppendLog($"{seat.Name} raids {target.Name}.");
                        }

                        break;
                    }
                }
            }
        }

        private void OnRollParty(int roll, ResourceBundle[] gained)
        {
            foreach (var seat in State.Seats)
            {
                if (seat.Civilization == CivilizationId.ForestFolk && (roll == 2 || roll == 12))
                    Give(seat, ResourceType.Wood, System.Math.Max(1, MatchPartyLibrary.Bordered(this, seat.Index, ResourceType.Wood)));
                if (seat.Trait == MatchTraitId.LuckyDice && (roll <= 3 || roll >= 11))
                    Give(seat, LeastHeld(seat.Hand), 1);
                if (seat.Trait == MatchTraitId.Prospector && gained[seat.Index].Total == 0 && seat.ProspectorRound != State.Round)
                {
                    Give(seat, LeastHeld(seat.Hand), 1);
                    seat.ProspectorRound = State.Round;
                }
            }
        }

        public static ResourceType LeastHeld(ResourceBundle hand) =>
            AllResources.OrderBy(r => hand[r]).ThenBy(r => (int)r).First();

        private static ResourceType MostHeld(ResourceBundle hand) =>
            AllResources.OrderByDescending(r => hand[r]).ThenBy(r => (int)r).First();

        private static void Give(MatchSeatState seat, ResourceType r, int n)
        {
            var h = seat.Hand;
            h.Add(r, n);
            seat.Hand = h;
        }

        /// <summary>Removes one random card from <paramref name="from"/>; gives it to <paramref name="to"/> (or the bank).</summary>
        private void TakeRandom(MatchSeatState from, MatchSeatState to)
        {
            var hand = from.Hand;
            int pick = State.PartyRng.Next(hand.Total);
            foreach (var r in AllResources)
            {
                if (pick < hand[r])
                {
                    Give(from, r, -1);
                    if (to != null)
                        Give(to, r, 1);
                    return;
                }

                pick -= hand[r];
            }
        }

        // ---------- events ----------

        /// <summary>Flip the next event card (start of every round from round 2).</summary>
        private void DrawEvent()
        {
            if (State.EventDeck.Count == 0)
                State.EventDeck.AddRange(Shuffled(MatchPartyLibrary.Events.Select(e => e.Id)));
            var id = State.EventDeck[State.EventDeck.Count - 1];
            State.EventDeck.RemoveAt(State.EventDeck.Count - 1);
            State.ActiveEvent = id;
            State.EventTile = null;
            var def = MatchPartyLibrary.Event(id);
            switch (id)
            {
                case EventId.Storm:
                {
                    var tiles = State.Board.Tiles.Values.Where(t => !t.IsDesert && t.NumberToken.HasValue)
                        .OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R).ToList();
                    if (tiles.Count > 0)
                        State.EventTile = tiles[State.PartyRng.Next(tiles.Count)].Coord;
                    break;
                }
                case EventId.BanditRaid:
                {
                    var busiest = State.Board.Tiles.Values
                        .Where(t => t.Coord != State.Board.RobberTile)
                        .OrderByDescending(t => BuildingsOn(t.Coord)).ThenBy(t => t.Coord.Q).ThenBy(t => t.Coord.R)
                        .FirstOrDefault();
                    if (busiest != null)
                        State.Board.PlaceRobber(busiest.Coord);
                    break;
                }
                case EventId.ResourceLevy:
                    foreach (var seat in State.Seats.Where(s => s.CardCount >= 8))
                        Give(seat, MostHeld(seat.Hand), -1);
                    break;
                case EventId.RoyalPatronage:
                {
                    int min = State.Seats.Min(s => s.PublicVictoryPoints);
                    foreach (var seat in State.Seats.Where(s => s.PublicVictoryPoints == min))
                        Give(seat, LeastHeld(seat.Hand), 1);
                    break;
                }
                case EventId.HarvestFestival:
                    foreach (var seat in State.Seats)
                        Give(seat, ResourceType.Wheat, 1);
                    break;
                case EventId.Chaos:
                    DoChaos();
                    break;
            }

            AppendLog($"Event – {def?.Name}: {def?.Description}");
            // v141: a Decree opens a secret ballot, the Night Market a sealed-bid auction (both before the first roll).
            if (id == EventId.Decree)
                BeginVote();
            else if (id == EventId.NightMarket)
                BeginAuction();
        }

        private int BuildingsOn(HexCoord hex)
        {
            int n = 0;
            for (int c = 0; c < 6; c++)
                if (State.Board.VertexBuildings.ContainsKey(VertexGraph.Canonicalize(new Vertex(hex, c))))
                    n++;
            return n;
        }

        // ---------- quests + win ----------

        /// <summary>Secret-quest progress of a seat (0..Target), for the owner's private readout.</summary>
        public int QuestProgress(int seat)
        {
            var def = MatchPartyLibrary.Quest(State.Seats[seat].Quest);
            return def == null ? 0 : State.Seats[seat].QuestDone ? def.Target : def.CurrentProgress(this, seat);
        }

        private void AfterPartyCommand()
        {
            if (!IsOver && !State.IsPicking)
            {
                bool changed = false;
                foreach (var seat in State.Seats)
                {
                    if (seat.Quest == MatchQuestId.None || seat.QuestDone)
                        continue;
                    if (MatchPartyLibrary.Quest(seat.Quest)?.IsComplete(this, seat.Index) == true)
                    {
                        seat.QuestDone = true;
                        changed = true;
                    }
                }

                if (changed)
                    RefreshVictoryPoints();
                if (!IsPregame && State.Current.VictoryPoints >= State.Config.VictoryPoints)
                {
                    State.Winner = State.CurrentSeat;
                    State.Phase = MatchPhase.GameOver;
                    AppendLog($"{State.Current.Name} wins with {State.Current.VictoryPoints} VP!");
                }
            }

            if (IsOver && !_questsRevealed)
            {
                _questsRevealed = true;
                foreach (var seat in State.Seats)
                {
                    var q = MatchPartyLibrary.Quest(seat.Quest);
                    if (q != null)
                        AppendLog($"{seat.Name}'s quest: {q.Name} ({q.Points} VP) – {(seat.QuestDone ? "done" : "missed")}.");
                }
            }
        }
    }
}
