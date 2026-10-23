using System.Linq;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v136 pass-and-play party rules on the table HUD: the secret civilization pick (3 leaders) before
    /// placement, the secret quest + trait pick after snake placement (last placer first), the round's event
    /// card, each player's private civ/quest panel, the robber victim picker and the game-over quest reveal.
    /// </summary>
    public sealed partial class RunHudController
    {
        /// <summary>
        /// v136 table framing (reference pixels): left of the hand/dev-card column, right of the seat strip, under
        /// the banner and above the action bar. The turn timer moved bottom-right so the board no longer reaches it.
        /// </summary>
        public static readonly ColonistPlayAreaLayout.Insets HotSeatPlayArea = new()
        {
            Left = 640f, Right = 400f, Top = 104f, Bottom = 142f
        };

        /// <summary>The robber pawn keeps base-board size on the 19/30-hex tables (it covered number tokens).</summary>
        public const float HotSeatRobberScaleCap = 1.35f;

        /// <summary>Moves the pawn off the token far enough for the larger cap to stay clear of it.</summary>
        public const float HotSeatRobberOffsetScale = 1.2f;

        private bool _lobbyPartyRules = true;
        private int _hsPickSeat = -1;
        private MatchQuestId _hsQuestChoice;
        private MatchTraitId _hsTraitChoice;
        private MatchCommand _hsVictimCommand;

        /// <summary>Lobby toggle: civilizations, secret quests and the event deck (on by default).</summary>
        public bool LobbyPartyRules
        {
            get => _lobbyPartyRules;
            set
            {
                _lobbyPartyRules = value;
                if (_hotSeatLobbyOpen)
                    RebuildPreGame();
            }
        }

        public bool HotSeatVictimPickerOpenForTests => _hsVictimCommand != null;

        public void SetHotSeatQuestChoiceForTests(MatchQuestId quest, MatchTraitId trait)
        {
            _hsPickSeat = _hotSeat?.State.ActingSeat ?? -1;
            _hsQuestChoice = quest;
            _hsTraitChoice = trait;
            HsMark();
        }

        /// <summary>Same path as a board click on a hex while the robber moves (opens the picker for 2+ victims).</summary>
        public void ClickHotSeatRobberHexForTests(HexCoord hex)
        {
            if (_hotSeat == null)
                return;
            int seat = _hotSeat.State.ActingSeat;
            var victims = _hotSeat.Match.RobberVictims(seat, hex).ToList();
            int victim = victims.Count == 0 ? -1 : victims.Count == 1 ? victims[0] : MatchBoardPick.VictimPending;
            var command = MatchCommand.Robber(seat, hex, victim);
            if (!InterceptHotSeatBoardCommand(command))
                _hotSeat.Apply(command);
            HsMark();
        }

        /// <summary>Board clicks: a robber move that touches 2+ opponents waits for the victim picker.</summary>
        private bool InterceptHotSeatBoardCommand(MatchCommand command)
        {
            if (command.Type != MatchCommandType.MoveRobber || command.Victim != MatchBoardPick.VictimPending)
                return false;
            _hsVictimCommand = command;
            HsMark();
            return true;
        }

        private bool HotSeatBoardBlocked() =>
            _hotSeat != null && (_hsVictimCommand != null || _hotSeat.State.IsPicking);

        // ---------- civilization pick ----------

        private void BuildPartyPickers()
        {
            var s = _hotSeat;
            var state = s.State;
            if (_hsVictimCommand != null && state.Phase != MatchPhase.MoveRobber)
                _hsVictimCommand = null;
            if (!state.IsPicking)
            {
                _hsPickSeat = -1;
                return;
            }

            int seat = state.ActingSeat;
            if (!s.IsHandVisible(seat))
                return;
            if (_hsPickSeat != seat)
            {
                _hsPickSeat = seat;
                _hsQuestChoice = MatchQuestId.None;
                _hsTraitChoice = MatchTraitId.None;
            }

            if (state.Phase == MatchPhase.CivPick)
                BuildCivPick(state.Seats[seat]);
            else
                BuildQuestPick(state.Seats[seat]);
        }

        private RectTransform PartyModal(string name, Vector2 size)
        {
            var dim = CreateImage(_hotSeatRoot, name + "Dim", null);
            dim.preserveAspect = false;
            dim.color = new Color(0.04f, 0.03f, 0.02f, 0.55f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            return HsModal(name, size, new Vector2(-190f, 6f));
        }

        private void BuildCivPick(MatchSeatState seat)
        {
            var card = PartyModal("CivPick", new Vector2(1280f, 800f));
            HsText(card, "Kicker", $"SECRET PICK  ·  ONLY {seat.Name.ToUpperInvariant()} LOOKS", 16, new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(900f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            HsText(card, "Title", $"{seat.Name}: choose your civilization", 32, new Vector2(0.5f, 1f),
                new Vector2(0f, -98f), new Vector2(1000f, 42f), ColonistUiKit.Ink, FontStyle.Bold);
            HsText(card, "Sub", "Picked before placement · revealed once everyone has chosen", 17, new Vector2(0.5f, 1f),
                new Vector2(0f, -132f), new Vector2(1000f, 24f), ColonistUiKit.InkMuted, FontStyle.Italic);

            var offer = seat.CivOffer;
            for (int i = 0; i < offer.Count; i++)
            {
                var civ = offer[i];
                if (!CivilizationLibrary.TryGet(civ, out var def))
                    continue;
                float x = (i - (offer.Count - 1) / 2f) * 392f;
                var panel = CreateImage(card, "Civ_" + i, null);
                panel.preserveAspect = false;
                ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
                SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -40f), new Vector2(372f, 560f));
                var rt = panel.rectTransform;
                var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);

                var portrait = CreateImage(rt, "Portrait", ColonistRoguelikeArt.GetCivilizationPortrait(civ));
                SetRect(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(170f, 170f));
                HsText(rt, "Name", def.Name, 26, new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(340f, 34f),
                    ink, FontStyle.Bold);
                HsText(rt, "Leader", $"led by {def.LeaderName}", 17, new Vector2(0.5f, 1f), new Vector2(0f, -252f),
                    new Vector2(340f, 24f), ColonistUiKit.InkMuted, FontStyle.Italic);
                var passive = HsText(rt, "Passive", "✦ " + MatchCivRules.Passive(civ), 18, new Vector2(0.5f, 1f),
                    new Vector2(0f, -318f), new Vector2(320f, 92f), ink);
                passive.horizontalOverflow = HorizontalWrapMode.Wrap;
                string drawback = MatchCivRules.Drawback(civ);
                HsText(rt, "Drawback", string.IsNullOrEmpty(drawback) ? "No drawback" : "▲ " + drawback, 16,
                    new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(320f, 60f),
                    string.IsNullOrEmpty(drawback) ? ColonistUiKit.InkMuted : new Color(0.62f, 0.16f, 0.1f, 1f),
                    FontStyle.Italic);
                int seatIndex = seat.Index;
                var c = civ;
                HsButton(rt, "PickCiv_" + i, "Lead the " + (def.Name.StartsWith("The ") ? def.Name.Substring(4) : def.Name), new Vector2(0.5f, 0f), new Vector2(0f, 52f),
                    new Vector2(310f, 56f), true, true, () => _hotSeat.Apply(MatchCommand.PickCiv(seatIndex, c)), 19);
            }
        }

        // ---------- secret quest + trait ----------

        private void BuildQuestPick(MatchSeatState seat)
        {
            var card = PartyModal("QuestPick", new Vector2(1280f, 820f));
            HsText(card, "Kicker", "SECRET QUEST  ·  HIDDEN FROM THE TABLE UNTIL THE GAME ENDS", 16, new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(1000f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            string civ = CivilizationLibrary.TryGet(seat.Civilization, out var def) ? def.Name : "";
            HsText(card, "Title", $"{seat.Name}: pick a quest and a trait", 32, new Vector2(0.5f, 1f),
                new Vector2(0f, -98f), new Vector2(1000f, 42f), ColonistUiKit.Ink, FontStyle.Bold);
            HsText(card, "Sub", $"{civ} · offers follow your starting spots · the last player to place picks first", 17,
                new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(1100f, 24f), ColonistUiKit.InkMuted,
                FontStyle.Italic);

            HsText(card, "QuestHead", "QUEST  ·  pick one (its points stay secret)", 18, new Vector2(0.5f, 1f),
                new Vector2(0f, -176f), new Vector2(900f, 24f), ColonistUiKit.Ink, FontStyle.Bold);
            var quests = seat.QuestOffer;
            for (int i = 0; i < quests.Count; i++)
            {
                var q = MatchPartyLibrary.Quest(quests[i]);
                if (q == null)
                    continue;
                bool chosen = _hsQuestChoice == q.Id;
                float x = (i - (quests.Count - 1) / 2f) * 392f;
                var id = q.Id;
                var b = HsButton(card, "Quest_" + i, "", new Vector2(0.5f, 1f), new Vector2(x, -310f),
                    new Vector2(372f, 220f), true, chosen, () => _hsQuestChoice = id);
                var rt = (RectTransform)b.transform;
                var ink = chosen ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                var badge = CreateDisc(rt, "Badge", q.Points >= 2 ? new Color(0.78f, 0.48f, 0.1f, 1f) : new Color(0.3f, 0.5f, 0.25f, 1f), 58f);
                SetRect(badge.rectTransform, new Vector2(0f, 1f), new Vector2(46f, -44f), new Vector2(58f, 58f));
                var pts = RunHudUiFactory.CreateText(badge.rectTransform, "Pts", $"{q.Points}\nVP", 16, TextAnchor.MiddleCenter,
                    ColonistUiKit.Cream, FontStyle.Bold);
                pts.lineSpacing = 0.8f;
                Stretch(pts.rectTransform);
                HsText(rt, "Name", q.Name, 23, new Vector2(0f, 1f), new Vector2(214f, -44f), new Vector2(270f, 30f), ink,
                    FontStyle.Bold, TextAnchor.MiddleLeft);
                HsText(rt, "Kind", q.Points >= 2 ? "harder" : "easier", 15, new Vector2(0f, 1f), new Vector2(214f, -68f),
                    new Vector2(270f, 20f), chosen ? ColonistUiKit.Cream : ColonistUiKit.InkMuted, FontStyle.Italic,
                    TextAnchor.MiddleLeft);
                HsText(rt, "Desc", q.Description, 18, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(330f, 72f),
                    ink);
                int now = q.CurrentProgress(_hotSeat.Match, seat.Index);
                HsText(rt, "Progress", $"now {now} / {q.Target}", 15, new Vector2(0.5f, 0f), new Vector2(0f, 26f),
                    new Vector2(330f, 20f), chosen ? ColonistUiKit.Cream : ColonistUiKit.InkMuted, FontStyle.Bold);
            }

            HsText(card, "TraitHead", "TRAIT  ·  pick one small edge", 18, new Vector2(0.5f, 1f), new Vector2(0f, -450f),
                new Vector2(900f, 24f), ColonistUiKit.Ink, FontStyle.Bold);
            var traits = seat.TraitOffer;
            for (int i = 0; i < traits.Count; i++)
            {
                var t = MatchPartyLibrary.Trait(traits[i]);
                if (t == null)
                    continue;
                bool chosen = _hsTraitChoice == t.Id;
                float x = (i - (traits.Count - 1) / 2f) * 520f;
                var id = t.Id;
                var b = HsButton(card, "Trait_" + i, "", new Vector2(0.5f, 1f), new Vector2(x, -548f),
                    new Vector2(500f, 140f), true, chosen, () => _hsTraitChoice = id);
                var rt = (RectTransform)b.transform;
                var ink = chosen ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                HsText(rt, "Name", t.Name, 22, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(460f, 30f), ink,
                    FontStyle.Bold);
                HsText(rt, "Desc", t.Description, 18, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f),
                    new Vector2(460f, 56f), ink);
            }

            bool ready = _hsQuestChoice != MatchQuestId.None && _hsTraitChoice != MatchTraitId.None;
            int seatIndex = seat.Index;
            HsButton(card, "QuestConfirm", ready ? "Keep it secret · confirm" : "Pick a quest and a trait",
                new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(360f, 58f), ready, true, () =>
                {
                    var q = _hsQuestChoice;
                    var t = _hsTraitChoice;
                    _hsQuestChoice = MatchQuestId.None;
                    _hsTraitChoice = MatchTraitId.None;
                    _hotSeat.Apply(MatchCommand.PickQuestTrait(seatIndex, q, t));
                }, 20);
        }

        // ---------- event card + private panel ----------

        private void BuildEventCard(MatchState state)
        {
            if (!state.Config.PartyRules || state.ActiveEvent == Core.Events.EventId.None)
                return;
            var ev = MatchPartyLibrary.Event(state.ActiveEvent);
            if (ev == null)
                return;
            var panel = CreateImage(_hotSeatRoot, "EventCard", null);
            panel.preserveAspect = false;
            ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
            SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(210f, -236f), new Vector2(380f, 236f));
            var rt = panel.rectTransform;
            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            var emblem = CreateImage(rt, "Emblem", ColonistRoguelikeArt.GetEventCategoryEmblem(ev.Category));
            SetRect(emblem.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -76f), new Vector2(84f, 84f));
            HsText(rt, "Kicker", $"ROUND {state.Round + 1} EVENT", 14, new Vector2(0f, 1f), new Vector2(248f, -50f),
                new Vector2(230f, 20f), ColonistUiKit.InkGold, FontStyle.Bold, TextAnchor.MiddleLeft);
            HsText(rt, "Name", ev.Name, 25, new Vector2(0f, 1f), new Vector2(248f, -80f), new Vector2(230f, 34f), ink,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            string desc = ev.Description;
            if (state.ActiveEvent == Core.Events.EventId.Storm && state.EventTile.HasValue
                && state.Board.TryGetTile(state.EventTile.Value, out var tile))
                desc = $"The storm sits on the {ResourceLabel(tile.Resource)} {tile.NumberToken}: it produces nothing this round.";
            HsText(rt, "Desc", desc, 17, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(330f, 92f), ink);
        }

        private void BuildPartyInfo()
        {
            var s = _hotSeat;
            var state = s.State;
            if (!state.Config.PartyRules || state.Phase == MatchPhase.CivPick)
                return;
            int seat = state.ActingSeat;
            if (!s.IsHandVisible(seat))
                return;
            var me = state.Seats[seat];
            if (!CivilizationLibrary.TryGet(me.Civilization, out var def))
                return;
            var panel = CreateImage(_hotSeatRoot, "PartyInfo", null);
            panel.preserveAspect = false;
            ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
            SetRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(330f, 446f), new Vector2(580f, 184f));
            var rt = panel.rectTransform;
            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            var portrait = CreateImage(rt, "Portrait", ColonistRoguelikeArt.GetCivilizationPortrait(me.Civilization));
            SetRect(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(96f, 96f));
            var trait = MatchPartyLibrary.Trait(me.Trait);
            HsText(rt, "Civ", def.Name + (trait != null ? $"  ·  {trait.Name}" : ""), 19, new Vector2(0f, 1f),
                new Vector2(340f, -30f), new Vector2(420f, 26f), ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            var passive = HsText(rt, "Passive", "✦ " + MatchCivRules.Passive(me.Civilization)
                    + (trait != null ? "\n✦ " + trait.Description : ""),
                14, new Vector2(0f, 1f), new Vector2(340f, -92f), new Vector2(420f, 72f), ColonistUiKit.InkMuted,
                FontStyle.Normal, TextAnchor.UpperLeft);
            passive.horizontalOverflow = HorizontalWrapMode.Wrap;
            passive.resizeTextForBestFit = true;
            passive.resizeTextMinSize = 11;
            passive.resizeTextMaxSize = 14;
            var quest = MatchPartyLibrary.Quest(me.Quest);
            string questLine = quest == null
                ? "Secret quest: picked after placement"
                : $"Secret quest: {quest.Name} ({quest.Points} VP)  ·  " +
                  (me.QuestDone ? "done ✓" : $"{quest.CurrentProgress(s.Match, seat)}/{quest.Target}");
            HsText(rt, "Quest", questLine, 17, new Vector2(0f, 0f), new Vector2(340f, 30f), new Vector2(420f, 24f),
                me.QuestDone ? new Color(0.2f, 0.45f, 0.16f, 1f) : ink, FontStyle.Bold, TextAnchor.MiddleLeft);
        }

        // ---------- robber victim picker ----------

        private void BuildVictimPicker()
        {
            var command = _hsVictimCommand;
            if (command == null)
                return;
            var state = _hotSeat.State;
            if (state.Phase != MatchPhase.MoveRobber || !_hotSeat.IsHandVisible(command.Seat))
            {
                _hsVictimCommand = null;
                return;
            }

            var victims = _hotSeat.Match.RobberVictims(command.Seat, command.Hex).ToList();
            // Docked over the left column so the board (and the chosen tile) stays in view.
            var card = HsModal("VictimPicker", new Vector2(600f, 250f + victims.Count * 78f), new Vector2(330f - 960f, 150f));
            string where = state.Board.TryGetTile(command.Hex, out var tile)
                ? $"THE ROBBER MOVES TO THE {ResourceLabel(tile.Resource).ToUpperInvariant()} {tile.NumberToken}"
                : "THE ROBBER MOVES";
            HsText(card, "Kicker", where, 16, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(520f, 22f),
                ColonistUiKit.InkGold, FontStyle.Bold);
            HsText(card, "Title", "Steal 1 random card from…", 28, new Vector2(0.5f, 1f), new Vector2(0f, -88f),
                new Vector2(520f, 40f), ColonistUiKit.Ink, FontStyle.Bold);
            for (int i = 0; i < victims.Count; i++)
            {
                int v = victims[i];
                var seat = state.Seats[v];
                float y = -150f - i * 78f;
                var disc = CreateDisc(card, "VictimDisc_" + v, PlayerPieceVisuals.ColorForSeat(v), 52f);
                SetRect(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(-190f, y), new Vector2(52f, 52f));
                HsButton(card, "Rob_" + v, $"{seat.Name}  ·  {seat.CardCount} card{(seat.CardCount == 1 ? "" : "s")}",
                    new Vector2(0.5f, 1f), new Vector2(50f, y), new Vector2(380f, 58f), true, true, () =>
                    {
                        _hsVictimCommand = null;
                        _hotSeat.Apply(MatchCommand.Robber(command.Seat, command.Hex, v));
                    }, 20);
            }

            HsButton(card, "VictimCancel", "Pick another tile", new Vector2(0.5f, 0f), new Vector2(0f, 58f),
                new Vector2(260f, 50f), true, false, () => _hsVictimCommand = null);
        }

        // ---------- game over reveal ----------

        private void BuildPartyGameOverRows(RectTransform card, MatchState state)
        {
            var ranked = state.Seats.OrderByDescending(x => x.VictoryPoints).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                var seat = ranked[i];
                float y = -206f - i * 84f;
                var portrait = CreateImage(card, "Portrait_" + seat.Index,
                    ColonistRoguelikeArt.GetCivilizationPortrait(seat.Civilization));
                SetRect(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(-380f, y), new Vector2(70f, 70f));
                var disc = CreateDisc(card, "Disc_" + seat.Index, PlayerPieceVisuals.ColorForSeat(seat.Index), 22f);
                SetRect(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(-354f, y - 24f), new Vector2(22f, 22f));
                string civ = CivilizationLibrary.TryGet(seat.Civilization, out var def) ? def.Name : "";
                HsText(card, "Rank_" + i, $"{i + 1}.  {seat.Name}  ·  {civ}  —  {seat.VictoryPoints} VP", 22,
                    new Vector2(0.5f, 1f), new Vector2(40f, y + 14f), new Vector2(760f, 30f), ColonistUiKit.Ink,
                    i == 0 ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft);
                var quest = MatchPartyLibrary.Quest(seat.Quest);
                var trait = MatchPartyLibrary.Trait(seat.Trait);
                string reveal = (quest == null ? "No quest" :
                                    $"Quest: {quest.Name} ({quest.Points} VP) " + (seat.QuestDone ? "✓ done" : "✗ missed"))
                                + (trait != null ? $"   ·   Trait: {trait.Name}" : "");
                HsText(card, "Quest_" + i, reveal, 18, new Vector2(0.5f, 1f), new Vector2(40f, y - 16f),
                    new Vector2(760f, 26f), seat.QuestDone ? new Color(0.2f, 0.45f, 0.16f, 1f) : ColonistUiKit.InkMuted,
                    FontStyle.Italic, TextAnchor.MiddleLeft);
            }
        }
    }
}
