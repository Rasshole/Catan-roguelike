using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v141 Phase 4 shenanigans on the pass-and-play table: the secret ballot (one private screen per voter, the
    /// totals revealed afterwards), the Night Market's sealed bids and its result, the scheme panel (bounties,
    /// sabotage cards, token rerolls), the saboteur guess and the sabotage reveal. Everything renders
    /// <see cref="HotSeatSession"/> state and forwards plain <see cref="MatchCommand"/>s.
    /// </summary>
    public sealed partial class RunHudController
    {
        public enum HotSeatScheme
        {
            None,
            Bounty,
            Sabotage,
            Reroll
        }

        private HotSeatScheme _hsScheme;
        private int _hsSchemeTarget = -1;
        private ResourceBundle _hsBountyReward;
        private MatchSabotageKind _hsSabotageKind;
        private int _hsSabotageSpot = -1;
        private ResourceBundle _hsBid;
        private int _hsBidSeat = -1;
        private int _hsBallotSeen = -1;
        private int _hsAuctionSeen = -1;
        private int _hsSabotageSeen;
        private int _hsGuessDismissed = -1;
        private bool _hsRevealShowing;

        /// <summary>A public reveal (ballot, auction, sabotage) waits for "Continue"; the board stays locked.</summary>
        public bool HotSeatRevealShowingForTests => _hsRevealShowing;

        public HotSeatScheme HotSeatSchemeForTests => _hsScheme;

        /// <summary>Tests/captures: open the scheme panel on a tab (as the "Schemes" button and the tabs would).</summary>
        public void OpenHotSeatSchemeForTests(HotSeatScheme scheme)
        {
            OpenScheme(scheme);
            HsMark();
        }

        private void OpenScheme(HotSeatScheme scheme)
        {
            _hsScheme = scheme;
            _hsSchemeTarget = -1;
            _hsBountyReward = ResourceBundle.Zero;
            _hsSabotageKind = MatchSabotageKind.None;
            _hsSabotageSpot = -1;
            if (scheme != HotSeatScheme.None)
            {
                _hsTradeOpen = false;
                _hsPicker = null;
            }
        }

        private void ResetShenanigansUi()
        {
            OpenScheme(HotSeatScheme.None);
            _hsBid = ResourceBundle.Zero;
            _hsBidSeat = -1;
            _hsBallotSeen = _hsAuctionSeen = -1;
            _hsSabotageSeen = 0;
            _hsGuessDismissed = -1;
        }

        /// <summary>The scheme panel is up (it takes the dev-card panel's space, like a trade).</summary>
        private bool HsSchemeOnTable =>
            _hotSeat != null && _hsScheme != HotSeatScheme.None && !_hotSeat.IsOver && !_hotSeat.PassScreenPending
            && _hotSeat.State.Phase == MatchPhase.Main && !_hotSeat.Acting.IsAi && _hotSeat.State.ActiveTrade == null;

        private bool PartyTable => _hotSeat != null && _hotSeat.State.Config.PartyRules;

        private void BuildShenanigans()
        {
            var s = _hotSeat;
            var state = s.State;
            _hsRevealShowing = false;
            if (!PartyTable)
                return;
            if (!HsSchemeOnTable)
                OpenSchemeIfKept();
            if (BuildPendingReveal(state))
            {
                _hsRevealShowing = true;
                return;
            }
            if (state.Phase == MatchPhase.Vote)
                BuildBallot(state);
            else if (state.Phase == MatchPhase.Auction)
                BuildBidCard(state);
            else if (state.Phase == MatchPhase.Roll)
                BuildGuessPanel(state);
            else if (HsSchemeOnTable)
                BuildSchemePanel(state);
        }

        private void OpenSchemeIfKept()
        {
            if (_hsScheme != HotSeatScheme.None)
                OpenScheme(HotSeatScheme.None);
        }

        private static string TileLabel(MatchState state, HexCoord hex) =>
            state.Board.TryGetTile(hex, out var t) ? $"{ResourceLabel(t.Resource)} {t.NumberToken}" : "a tile";

        /// <summary>A row of leader portraits, one per seat in <paramref name="seats"/>, each with a caption.</summary>
        private void PortraitRow(RectTransform card, string key, IList<int> seats, float y, float pitch, float size,
            System.Func<int, string> caption, System.Func<int, Color> captionInk = null, System.Action<int> click = null,
            int selected = -1)
        {
            for (int i = 0; i < seats.Count; i++)
            {
                int seat = seats[i];
                float x = (i - (seats.Count - 1) / 2f) * pitch;
                if (seat == selected)
                {
                    var sel = CreateDisc(card, key + "Sel_" + seat, new Color(1f, 0.85f, 0.35f, 1f), size + 16f);
                    SetRect(sel.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(size + 16f, size + 16f));
                }

                var img = HsTradePortrait(card, key + "_" + seat, seat, new Vector2(0.5f, 1f), new Vector2(x, y), size);
                if (click != null)
                {
                    var b = img.gameObject.AddComponent<Button>();
                    b.targetGraphic = img;
                    int v = seat;
                    b.onClick.AddListener(() =>
                    {
                        click(v);
                        HsMark();
                    });
                }

                string text = caption?.Invoke(seat);
                if (!string.IsNullOrEmpty(text))
                    HsText(card, key + "Cap_" + seat, text, 16, new Vector2(0.5f, 1f), new Vector2(x, y - size / 2f - 18f),
                        new Vector2(pitch - 6f, 22f), captionInk?.Invoke(seat) ?? ColonistUiKit.InkMuted,
                        seat == selected ? FontStyle.Bold : FontStyle.Italic);
            }
        }

        // ---------- secret ballot ----------

        private void BuildBallot(MatchState state)
        {
            int seat = state.ActingSeat;
            if (!_hotSeat.IsHandVisible(seat))
                return;
            var me = state.Seats[seat];
            var card = PartyModal("Ballot", new Vector2(960f, 620f));
            HsText(card, "Kicker", $"SECRET BALLOT  ·  ONLY {me.Name.ToUpperInvariant()} LOOKS", 16, new Vector2(0.5f, 1f),
                new Vector2(0f, -58f), new Vector2(820f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            var emblem = CreateImage(card, "Emblem",
                ColonistRoguelikeArt.GetEventCategoryEmblem(MatchPartyLibrary.Event(Core.Events.EventId.Decree)?.Category ?? default));
            SetRect(emblem.rectTransform, new Vector2(0.5f, 1f), new Vector2(-330f, -128f), new Vector2(84f, 84f));
            HsText(card, "Title", $"Decree: {MatchController.DecreeName(state.BallotDecree)}", 34, new Vector2(0.5f, 1f),
                new Vector2(30f, -110f), new Vector2(640f, 44f), ColonistUiKit.Ink, FontStyle.Bold);
            var text = HsText(card, "Decree", MatchController.DecreeText(state.BallotDecree), 21, new Vector2(0.5f, 1f),
                new Vector2(30f, -158f), new Vector2(640f, 56f), ColonistUiKit.Ink);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var all = Enumerable.Range(0, state.Seats.Count).ToList();
            HsText(card, "VotedHead", "WHO HAS VOTED  ·  votes stay sealed until everyone is done", 15, new Vector2(0.5f, 1f),
                new Vector2(0f, -226f), new Vector2(820f, 20f), ColonistUiKit.InkMuted, FontStyle.Bold);
            PortraitRow(card, "Voter", all, -300f, 150f, 76f, i =>
                    i == seat ? "voting now" : state.PickOrder.IndexOf(i) < state.PickStep ? "✓ sealed" : "waiting",
                i => i == seat ? ColonistUiKit.Ink : state.PickOrder.IndexOf(i) < state.PickStep
                    ? new Color(0.2f, 0.45f, 0.16f, 1f) : ColonistUiKit.InkMuted, selected: seat);
            HsText(card, "Rule", "Majority decides · a tie goes the way of the player with the fewest points.", 16,
                new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(820f, 22f), ColonistUiKit.InkMuted, FontStyle.Italic);
            int v = seat;
            HsButton(card, "VoteAye", "✓  Aye", new Vector2(0.5f, 0f), new Vector2(-140f, 74f), new Vector2(240f, 64f), true,
                true, () => _hotSeat.Apply(MatchCommand.Vote(v, true)), 24);
            HsButton(card, "VoteNay", "✕  Nay", new Vector2(0.5f, 0f), new Vector2(140f, 74f), new Vector2(240f, 64f), true,
                false, () => _hotSeat.Apply(MatchCommand.Vote(v, false)), 24);
        }

        // ---------- Night Market ----------

        private static string AuctionItemText(MatchAuctionItem item) => item switch
        {
            MatchAuctionItem.TokenReroll => "Reroll the number on one of your own tiles, any time on your turn.",
            MatchAuctionItem.SabotageCard => "A face-down sabotage card from the deck (not for sale in the shop this cheap).",
            MatchAuctionItem.VictoryPointCard => "A hidden Victory Point card: 1 VP nobody else can see.",
            _ => ""
        };

        private static string Title(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private void BuildBidCard(MatchState state)
        {
            int seat = state.ActingSeat;
            if (!_hotSeat.IsHandVisible(seat))
                return;
            if (_hsBidSeat != seat)
            {
                _hsBidSeat = seat;
                _hsBid = ResourceBundle.Zero;
            }

            var me = state.Seats[seat];
            var card = PartyModal("NightMarket", new Vector2(960f, 700f));
            HsText(card, "Kicker", $"NIGHT MARKET  ·  SEALED BID  ·  ONLY {me.Name.ToUpperInvariant()} LOOKS", 16,
                new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(820f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            var emblem = CreateImage(card, "Emblem",
                ColonistRoguelikeArt.GetEventCategoryEmblem(MatchPartyLibrary.Event(Core.Events.EventId.NightMarket)?.Category ?? default));
            SetRect(emblem.rectTransform, new Vector2(0.5f, 1f), new Vector2(-330f, -128f), new Vector2(84f, 84f));
            HsText(card, "Title", Title(MatchController.AuctionItemName(state.AuctionItem)), 34, new Vector2(0.5f, 1f),
                new Vector2(30f, -110f), new Vector2(640f, 44f), ColonistUiKit.Ink, FontStyle.Bold);
            var desc = HsText(card, "Item", AuctionItemText(state.AuctionItem), 20, new Vector2(0.5f, 1f),
                new Vector2(30f, -158f), new Vector2(640f, 52f), ColonistUiKit.Ink);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            var all = Enumerable.Range(0, state.Seats.Count).ToList();
            PortraitRow(card, "Bidder", all, -262f, 150f, 64f, i =>
                    i == seat ? "bidding now" : state.PickOrder.IndexOf(i) < state.PickStep ? "✓ sealed" : "waiting",
                i => i == seat ? ColonistUiKit.Ink : state.PickOrder.IndexOf(i) < state.PickStep
                    ? new Color(0.2f, 0.45f, 0.16f, 1f) : ColonistUiKit.InkMuted, selected: seat);

            HsText(card, "BidHead", "YOUR SEALED BID  ·  click a card to add it, right-click or − to take it back", 15,
                new Vector2(0.5f, 1f), new Vector2(0f, -352f), new Vector2(820f, 20f), ColonistUiKit.InkMuted, FontStyle.Bold);
            float x0 = TradeRowX0(HsTradeWidth) + (960f - HsTradeWidth) / 2f - 30f;
            BuildTradeEditRow(card, "Bid", true, -430f, x0, () => _hsBid, b => _hsBid = b, me.Hand, HsMark);
            HsText(card, "Rule", "Highest bid wins and pays the second-highest bid (at least 1 card). Losers pay nothing.", 16,
                new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(860f, 22f), ColonistUiKit.InkMuted, FontStyle.Italic);
            int v = seat;
            int total = _hsBid.Total;
            // v142: every lot has a minimum price (VP card 3, sabotage 2, reroll 1).
            int reserve = MatchController.ReservePrice(state.AuctionItem);
            string sealLabel = total <= 0 ? $"Add cards to bid · min {reserve}"
                : total < reserve ? $"Minimum bid · {reserve} cards"
                : $"Seal bid · {total} card{(total == 1 ? "" : "s")}";
            HsButton(card, "SealBid", sealLabel,
                new Vector2(0.5f, 0f), new Vector2(-140f, 74f), new Vector2(280f, 62f), total >= reserve && total > 0 && me.Hand.CanAfford(_hsBid),
                true, () =>
                {
                    var bid = _hsBid;
                    _hsBid = ResourceBundle.Zero;
                    _hotSeat.Apply(MatchCommand.Bid(v, bid));
                }, 21);
            HsButton(card, "PassBid", "Pass", new Vector2(0.5f, 0f), new Vector2(150f, 74f), new Vector2(220f, 62f), true, false,
                () =>
                {
                    _hsBid = ResourceBundle.Zero;
                    _hotSeat.Apply(MatchCommand.Bid(v, ResourceBundle.Zero));
                }, 21);
        }

        // ---------- reveals (public, UI-only acknowledgement) ----------

        private bool BuildPendingReveal(MatchState state)
        {
            if (_hotSeat.PassScreenPending || _hotSeat.IsOver || state.IsBallot || _hotSeat.Acting.IsAi)
                return false;
            var ballot = state.LastBallot;
            if (ballot != null && ballot.Round == state.Round && _hsBallotSeen != ballot.Round)
            {
                BuildBallotReveal(state, ballot);
                return true;
            }

            var auction = state.LastAuction;
            if (auction != null && auction.Round == state.Round && _hsAuctionSeen != auction.Round)
            {
                BuildAuctionResult(state, auction);
                return true;
            }

            var sab = state.LastSabotageReveal;
            if (sab != null && sab.Serial > _hsSabotageSeen)
            {
                BuildSabotageReveal(state, sab);
                return true;
            }

            return false;
        }

        private void BuildBallotReveal(MatchState state, MatchBallotResult ballot)
        {
            var card = PartyModal("BallotReveal", new Vector2(900f, 560f));
            HsText(card, "Kicker", "THE BALLOT IS COUNTED", 16, new Vector2(0.5f, 1f), new Vector2(0f, -58f),
                new Vector2(760f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            HsText(card, "Title", $"{MatchController.DecreeName(ballot.Decree)} {(ballot.Passed ? "passes" : "fails")}", 38,
                new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(760f, 48f),
                ballot.Passed ? new Color(0.2f, 0.42f, 0.14f, 1f) : new Color(0.62f, 0.16f, 0.1f, 1f), FontStyle.Bold);
            HsText(card, "Ayes", ballot.Yes.ToString(), 64, new Vector2(0.5f, 1f), new Vector2(-150f, -200f),
                new Vector2(160f, 76f), ColonistUiKit.Ink, FontStyle.Bold);
            HsText(card, "AyeLabel", "AYE", 16, new Vector2(0.5f, 1f), new Vector2(-150f, -248f), new Vector2(160f, 22f),
                ColonistUiKit.InkMuted, FontStyle.Bold);
            HsText(card, "Nays", ballot.No.ToString(), 64, new Vector2(0.5f, 1f), new Vector2(150f, -200f),
                new Vector2(160f, 76f), ColonistUiKit.Ink, FontStyle.Bold);
            HsText(card, "NayLabel", "NAY", 16, new Vector2(0.5f, 1f), new Vector2(150f, -248f), new Vector2(160f, 22f),
                ColonistUiKit.InkMuted, FontStyle.Bold);
            HsText(card, "Dash", "–", 48, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(60f, 70f),
                ColonistUiKit.InkMuted, FontStyle.Bold);
            string what = ballot.Passed
                ? ballot.Decree == MatchDecreeId.RichestDiscardsToSeven
                    ? "The tax is collected at once: the richest hand drops to 7."
                    : MatchController.DecreeText(ballot.Decree) + " It ends at nightfall."
                : "Nothing changes today.";
            var body = HsText(card, "Effect", what, 20, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(760f, 52f),
                ColonistUiKit.Ink);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (ballot.TieBreaker >= 0)
            {
                HsTradePortrait(card, "TieBreaker", ballot.TieBreaker, new Vector2(0.5f, 1f), new Vector2(-210f, -376f), 56f);
                HsText(card, "Tie", $"Tie: {state.Seats[ballot.TieBreaker].Name}, with the fewest points, decided.", 18,
                    new Vector2(0.5f, 1f), new Vector2(40f, -376f), new Vector2(460f, 26f), ColonistUiKit.InkMuted,
                    FontStyle.Italic, TextAnchor.MiddleLeft);
            }
            else
                HsText(card, "Secret", "Who voted which way stays secret.", 18, new Vector2(0.5f, 1f),
                    new Vector2(0f, -376f), new Vector2(760f, 26f), ColonistUiKit.InkMuted, FontStyle.Italic);

            int round = ballot.Round;
            HsButton(card, "RevealContinue", "Continue", new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(260f, 60f),
                true, true, () => _hsBallotSeen = round, 22);
        }

        private void BuildAuctionResult(MatchState state, MatchAuctionResult result)
        {
            var card = PartyModal("AuctionResult", new Vector2(900f, 540f));
            HsText(card, "Kicker", "NIGHT MARKET  ·  THE BIDS ARE OPENED", 16, new Vector2(0.5f, 1f), new Vector2(0f, -58f),
                new Vector2(760f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            string item = MatchController.AuctionItemName(result.Item);
            if (result.Winner < 0)
            {
                HsText(card, "Title", "No bids tonight", 36, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                    new Vector2(760f, 46f), ColonistUiKit.Ink, FontStyle.Bold);
                HsText(card, "Body", $"{Title(item)} goes back under the counter.", 20, new Vector2(0.5f, 1f),
                    new Vector2(0f, -170f), new Vector2(760f, 30f), ColonistUiKit.InkMuted, FontStyle.Italic);
            }
            else
            {
                var w = state.Seats[result.Winner];
                var ring = CreateDisc(card, "WinnerGlow", new Color(1f, 0.85f, 0.35f, 1f), 160f);
                SetRect(ring.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(160f, 160f));
                HsTradePortrait(card, "Winner", result.Winner, new Vector2(0.5f, 1f), new Vector2(0f, -190f), 140f);
                HsText(card, "Title", $"{w.Name} wins {item}", 32, new Vector2(0.5f, 1f), new Vector2(0f, -302f),
                    new Vector2(800f, 42f), ColonistUiKit.Ink, FontStyle.Bold);
                HsText(card, "Price",
                    $"Pays {result.Price} card{(result.Price == 1 ? "" : "s")} (the second-highest bid)  ·  {result.Bidders} sealed bid{(result.Bidders == 1 ? "" : "s")}",
                    19, new Vector2(0.5f, 1f), new Vector2(0f, -346f), new Vector2(800f, 26f), ColonistUiKit.InkMuted,
                    FontStyle.Italic);
            }

            int round = result.Round;
            HsButton(card, "AuctionContinue", "Continue", new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(260f, 60f),
                true, true, () => _hsAuctionSeen = round, 22);
        }

        private void BuildSabotageReveal(MatchState state, MatchSabotageReveal r)
        {
            var card = HsModal("SabotageReveal", new Vector2(640f, 400f), new Vector2(330f - 960f, HsTradeOffsetY(400f)));
            HsText(card, "Kicker", r.Cancelled ? "UNMASKED" : "SABOTAGE REVEALED", 16, new Vector2(0.5f, 1f),
                new Vector2(0f, -56f), new Vector2(460f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            BuildSabotageCardFace(card, "Card", r.Kind, new Vector2(0f, 1f), new Vector2(172f, -186f), false, false);
            HsTradePortrait(card, "Author", r.Author, new Vector2(0f, 1f), new Vector2(345f, -140f), 62f);
            HsTradePortrait(card, "Target", r.Target, new Vector2(0f, 1f), new Vector2(495f, -140f), 62f);
            HsText(card, "Arrow", "→", 30, new Vector2(0f, 1f), new Vector2(420f, -140f), new Vector2(50f, 40f),
                ColonistUiKit.InkMuted, FontStyle.Bold);
            string line = r.Cancelled
                ? $"{state.Seats[r.Target].Name} named {state.Seats[r.Author].Name}: the card is torn up."
                : $"{state.Seats[r.Author].Name} played it on {state.Seats[r.Target].Name}.";
            var t = HsText(card, "Line", line, 18, new Vector2(0f, 1f), new Vector2(420f, -224f), new Vector2(250f, 76f),
                ColonistUiKit.Ink, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            int serial = r.Serial;
            HsButton(card, "SabotageContinue", "Continue", new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(220f, 52f),
                true, true, () => _hsSabotageSeen = serial, 20);
        }

        /// <summary>A painted sabotage card (name + effect), optionally a button.</summary>
        private RectTransform BuildSabotageCardFace(RectTransform parent, string name, MatchSabotageKind kind, Vector2 anchor,
            Vector2 pos, bool selected, bool clickable, System.Action click = null)
        {
            var img = CreateImage(parent, name, null);
            img.preserveAspect = false;
            ColonistUiKit.Apply(img, UiKitPiece.FrameMedium);
            if (selected)
                img.color = new Color(1f, 0.88f, 0.55f, 1f);
            SetRect(img.rectTransform, anchor, pos, new Vector2(170f, 200f));
            var rt = img.rectTransform;
            var band = CreateImage(rt, "Band", null);
            band.color = new Color(0.42f, 0.12f, 0.1f, 0.92f);
            SetRect(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(136f, 34f));
            HsText(rt, "Name", MatchController.SabotageName(kind), kind == MatchSabotageKind.WashedOutRoad ? 15 : 18,
                new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(132f, 30f), ColonistUiKit.Cream, FontStyle.Bold);
            var desc = HsText(rt, "Text", MatchController.SabotageText(kind), 14, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -22f), new Vector2(132f, 110f), ColonistUiKit.TextOn(UiKitPiece.FrameMedium));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (clickable)
            {
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() =>
                {
                    click?.Invoke();
                    HsMark();
                });
            }

            return rt;
        }

        // ---------- saboteur guess (start of your turn) ----------

        private void BuildGuessPanel(MatchState state)
        {
            int seat = state.ActingSeat;
            if (!_hotSeat.IsHandVisible(seat) || !_hotSeat.Match.CanGuessSaboteur(seat) || _hsGuessDismissed == state.TurnNumber)
                return;
            var others = Enumerable.Range(0, state.Seats.Count).Where(i => i != seat).ToList();
            var card = HsModal("GuessPanel", new Vector2(640f, 420f), new Vector2(330f - 960f, HsTradeOffsetY(420f)));
            HsText(card, "Kicker", "A FACE-DOWN CARD LIES ON YOU", 16, new Vector2(0.5f, 1f), new Vector2(0f, -56f),
                new Vector2(460f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            var t = HsText(card, "Title", "Name the saboteur? Pay 1 card: right cancels it and unmasks them.", 19,
                new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(450f, 56f), ColonistUiKit.Ink, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var hand = state.Seats[seat].Hand;
            PortraitRow(card, "Guess", others, -204f, Mathf.Min(120f, 440f / others.Count), 66f, i => state.Seats[i].Name,
                click: i => _hotSeat.Apply(MatchCommand.Guess(seat, i, MatchController.SuggestDiscard(hand, 1))));
            HsButton(card, "GuessLater", "Not now · roll", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(240f, 50f),
                true, false, () => _hsGuessDismissed = state.TurnNumber);
        }

        // ---------- scheme panel: bounties, sabotage, rerolls ----------

        private void BuildSchemePanel(MatchState state)
        {
            int seat = state.ActingSeat;
            if (!_hotSeat.IsHandVisible(seat))
                return;
            const float h = 640f;
            var card = HsModal("SchemePanel", new Vector2(HsTradeWidth, h), new Vector2(HsTradeX, HsTradeOffsetY(h)));
            var me = state.Seats[seat];
            var tabs = new[] { HotSeatScheme.Bounty, HotSeatScheme.Sabotage, HotSeatScheme.Reroll };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                string label = tab switch
                {
                    HotSeatScheme.Bounty => "Bounty",
                    HotSeatScheme.Sabotage => $"Sabotage · {me.Sabotage.Count}",
                    _ => $"Reroll · {me.Rerolls}"
                };
                HsButton(card, "SchemeTab_" + tab, label, new Vector2(0.5f, 1f), new Vector2(-210f + i * 196f, -62f),
                    new Vector2(184f, 48f), true, _hsScheme == tab, () => OpenScheme(tab), 18);
            }

            HsButton(card, "SchemeClose", "✕", new Vector2(1f, 1f), new Vector2(-62f, -62f), new Vector2(52f, 48f), true, false,
                () => OpenScheme(HotSeatScheme.None), 20);
            switch (_hsScheme)
            {
                case HotSeatScheme.Bounty:
                    BuildBountyTab(card, state, seat);
                    break;
                case HotSeatScheme.Sabotage:
                    BuildSabotageTab(card, state, seat);
                    break;
                default:
                    BuildRerollTab(card, state, seat);
                    break;
            }
        }

        private void BuildBountyTab(RectTransform card, MatchState state, int seat)
        {
            var match = _hotSeat.Match;
            var me = state.Seats[seat];
            int actEnd = (state.Round / MatchController.ActLength + 1) * MatchController.ActLength;
            HsText(card, "Kicker", "POST A BOUNTY", 16, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(620f, 22f),
                ColonistUiKit.InkGold, FontStyle.Bold);
            var sub = HsText(card, "Sub",
                $"Whoever robs your mark first (robber on their tile + a steal) collects it. Unclaimed, it comes back after round {actEnd}.",
                17, new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(540f, 56f), ColonistUiKit.InkMuted, FontStyle.Italic);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            var mine = match.BountyBy(seat);
            if (mine != null)
            {
                HsTradePortrait(card, "MyMark", mine.Target, new Vector2(0.5f, 1f), new Vector2(0f, -260f), 96f);
                HsText(card, "MyBounty", $"Your bounty of {mine.Reward.Total} is on {state.Seats[mine.Target].Name}.", 22,
                    new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(620f, 30f), ColonistUiKit.Ink, FontStyle.Bold);
                HsText(card, "OneAtATime", "One bounty at a time.", 17, new Vector2(0.5f, 1f), new Vector2(0f, -372f),
                    new Vector2(620f, 24f), ColonistUiKit.InkMuted, FontStyle.Italic);
                return;
            }

            var others = Enumerable.Range(0, state.Seats.Count).Where(i => i != seat).ToList();
            PortraitRow(card, "Mark", others, -246f, Mathf.Min(140f, 640f / others.Count), 74f, i =>
                {
                    int on = match.BountiesOn(i).Sum(b => b.Reward.Total);
                    return state.Seats[i].Name + (on > 0 ? $" · {on} on" : "");
                }, null, i => _hsSchemeTarget = i, _hsSchemeTarget);

            // The reward row: cards from your hand, 1–3 in total.
            int room = MatchController.MaxBountyReward - _hsBountyReward.Total;
            var limit = ResourceBundle.Zero;
            foreach (var r in MatchController.AllResources)
                limit.Set(r, Mathf.Min(me.Hand[r], _hsBountyReward[r] + room));
            HsText(card, "RewardHead", $"REWARD  ·  1–{MatchController.MaxBountyReward} cards from your hand, held in escrow", 15,
                new Vector2(0.5f, 1f), new Vector2(0f, -348f), new Vector2(620f, 20f), ColonistUiKit.InkMuted, FontStyle.Bold);
            BuildTradeEditRow(card, "Bounty", true, -424f, TradeRowX0(HsTradeWidth), () => _hsBountyReward,
                b => _hsBountyReward = b, limit, HsMark);
            int total = _hsBountyReward.Total;
            bool ok = _hsSchemeTarget >= 0 && total >= 1 && total <= MatchController.MaxBountyReward && me.Hand.CanAfford(_hsBountyReward);
            string who = _hsSchemeTarget >= 0 ? state.Seats[_hsSchemeTarget].Name : "…";
            HsButton(card, "PostBounty", ok ? $"Post {total} on {who}" : "Pick a mark and a reward", new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(320f, 56f), ok, true, () =>
                {
                    var reward = _hsBountyReward;
                    int target = _hsSchemeTarget;
                    if (_hotSeat.Apply(MatchCommand.Bounty(seat, target, reward)).Ok)
                        OpenScheme(HotSeatScheme.None);
                }, 20);
        }

        private void BuildSabotageTab(RectTransform card, MatchState state, int seat)
        {
            var match = _hotSeat.Match;
            var me = state.Seats[seat];
            HsText(card, "Kicker", $"SABOTAGE  ·  {me.Sabotage.Count}/{MatchController.MaxSabotageHeld} held", 16,
                new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(620f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            HsButton(card, "BuySabotage", "Buy one · 1 Grain + 1 Ore", new Vector2(1f, 1f), new Vector2(-210f, -150f),
                new Vector2(260f, 44f), match.CanBuySabotage(seat), false,
                () => _hotSeat.Apply(MatchCommand.Simple(MatchCommandType.BuySabotage, seat)), 17);
            var kinds = me.Sabotage.Distinct().ToList();
            if (!kinds.Contains(_hsSabotageKind))
                _hsSabotageKind = kinds.Count > 0 ? kinds[0] : MatchSabotageKind.None;
            if (kinds.Count == 0)
            {
                var none = HsText(card, "NoCards",
                    "No sabotage cards. Buy one, or win one at the Night Market. Played face-down, the victim learns who it was only when it bites (or if they guess).",
                    18, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(540f, 110f), ColonistUiKit.InkMuted,
                    FontStyle.Italic);
                none.horizontalOverflow = HorizontalWrapMode.Wrap;
                return;
            }

            // Fan the held cards on the left; the selected one is drawn last so it sits on top.
            float cardPitch = kinds.Count > 1 ? 120f : 184f;
            var order = Enumerable.Range(0, kinds.Count).OrderBy(i => kinds[i] == _hsSabotageKind ? 1 : 0).ToList();
            foreach (int i in order)
            {
                var k = kinds[i];
                int count = me.Sabotage.Count(x => x == k);
                var face = BuildSabotageCardFace(card, "SabCard_" + k, k, new Vector2(0f, 1f), new Vector2(175f + i * cardPitch, -288f),
                    _hsSabotageKind == k, true, () =>
                    {
                        _hsSabotageKind = k;
                        _hsSabotageSpot = -1;
                    });
                if (count > 1)
                    HsText(face, "Count", "×" + count, 16, new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(40f, 22f),
                        ColonistUiKit.Ink, FontStyle.Bold);
            }

            var kind = _hsSabotageKind;
            bool faceDown = kind != MatchSabotageKind.WashedOutRoad;
            var targets = Enumerable.Range(0, state.Seats.Count)
                .Where(i => i != seat && (!faceDown || match.PendingSabotageOn(i) == null)).ToList();
            if (_hsSchemeTarget >= 0 && !targets.Contains(_hsSchemeTarget))
                _hsSchemeTarget = -1;
            float tx = 175f + (kinds.Count - 1) * cardPitch + 95f;
            const float right = 655f;
            HsText(card, "TargetHead", "TARGET", 15, new Vector2(0f, 1f), new Vector2(tx + (right - tx) / 2f, -196f),
                new Vector2(right - tx, 20f), ColonistUiKit.InkMuted, FontStyle.Bold);
            for (int i = 0; i < targets.Count; i++)
            {
                int t = targets[i];
                float pitch = Mathf.Min(96f, (right - tx) / Mathf.Max(1, targets.Count));
                float x = tx + pitch / 2f + i * pitch;
                if (t == _hsSchemeTarget)
                {
                    var sel = CreateDisc(card, "TargetSel_" + t, new Color(1f, 0.85f, 0.35f, 1f), 76f);
                    SetRect(sel.rectTransform, new Vector2(0f, 1f), new Vector2(x, -246f), new Vector2(76f, 76f));
                }

                var img = HsTradePortrait(card, "Target_" + t, t, new Vector2(0f, 1f), new Vector2(x, -246f), 62f);
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() =>
                {
                    _hsSchemeTarget = t;
                    _hsSabotageSpot = -1;
                    HsMark();
                });
                HsText(card, "TargetName_" + t, state.Seats[t].Name, 14, new Vector2(0f, 1f), new Vector2(x, -292f),
                    new Vector2(pitch, 20f), ColonistUiKit.InkMuted, t == _hsSchemeTarget ? FontStyle.Bold : FontStyle.Italic);
            }

            int target = _hsSchemeTarget;
            List<HexCoord> hexes = null;
            List<HexMath.Edge> roads = null;
            if (target >= 0 && kind == MatchSabotageKind.Blight)
                hexes = match.BlightTiles(target).ToList();
            if (target >= 0 && kind == MatchSabotageKind.WashedOutRoad)
                roads = match.WashableRoads(target).ToList();
            int spots = hexes?.Count ?? roads?.Count ?? 0;
            if (_hsSabotageSpot >= spots)
                _hsSabotageSpot = -1;
            if (spots > 0)
            {
                HsText(card, "SpotHead", kind == MatchSabotageKind.Blight ? "WHICH HEX" : "WHICH ROAD", 15,
                    new Vector2(0f, 1f), new Vector2(tx + (right - tx) / 2f, -322f), new Vector2(right - tx, 20f),
                    ColonistUiKit.InkMuted, FontStyle.Bold);
                int shown = Mathf.Min(spots, 6);
                for (int i = 0; i < shown; i++)
                {
                    int idx = i;
                    float bw = (right - tx - 10f) / 2f;
                    // v142: the spot reads like the board — resource icon + number token — instead of "Lumber 10".
                    var spot = HsButton(card, "Spot_" + i, "", new Vector2(0f, 1f),
                        new Vector2(tx + bw / 2f + (i % 2) * (bw + 10f), -360f - (i / 2) * 50f), new Vector2(bw, 42f), true,
                        _hsSabotageSpot == i, () => _hsSabotageSpot = idx, 15);
                    var spotHexes = hexes != null ? new List<HexCoord> { hexes[i] } : RoadHexes(state, roads[i]);
                    BuildSpotFace((RectTransform)spot.transform, state, spotHexes, hexes == null);
                    var accessible = spot.GetComponentInChildren<Text>();
                    if (accessible != null)
                        accessible.name = hexes != null ? TileLabel(state, hexes[i]) : RoadLabel(state, roads[i]);
                }
            }

            bool needSpot = kind != MatchSabotageKind.SpoiledGrain;
            bool ready = target >= 0 && (!needSpot || _hsSabotageSpot >= 0);
            string playLabel = !ready ? "Pick a target" + (needSpot ? " and a spot" : "")
                : faceDown ? $"Play face-down on {state.Seats[target].Name}" : $"Wash out {state.Seats[target].Name}'s road";
            var hint = HsText(card, "PlayHint", faceDown
                    ? "Face-down: it bites at their next roll, and nobody learns it was you unless they guess."
                    : "Takes effect at once and is signed: the road stops counting until nightfall.", 16,
                new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(540f, 44f), ColonistUiKit.InkMuted, FontStyle.Italic);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            HsButton(card, "PlaySabotage", playLabel, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(380f, 56f), ready,
                true, () =>
                {
                    var command = kind == MatchSabotageKind.WashedOutRoad
                        ? MatchCommand.SabotageRoad(seat, target, roads[_hsSabotageSpot])
                        : MatchCommand.Sabotage(seat, kind, target,
                            kind == MatchSabotageKind.Blight ? hexes[_hsSabotageSpot] : default);
                    if (_hotSeat.Apply(command).Ok)
                        OpenScheme(HotSeatScheme.None);
                }, 19);
        }

        /// <summary>v142: the land hexes on either side of a road (one on the coast).</summary>
        private static List<HexCoord> RoadHexes(MatchState state, HexMath.Edge e)
        {
            var list = new List<HexCoord>();
            if (state.Board.TryGetTile(e.A.Hex, out _))
                list.Add(e.A.Hex);
            if (e.B.Hex != e.A.Hex && state.Board.TryGetTile(e.B.Hex, out _))
                list.Add(e.B.Hex);
            return list;
        }

        /// <summary>
        /// v142: a sabotage spot button's face — for each hex its resource icon and a board-style number token
        /// (red 6 / 8), a road glyph in front when it is a road. Centered in the button.
        /// </summary>
        private void BuildSpotFace(RectTransform button, MatchState state, List<HexCoord> hexes, bool road)
        {
            const float icon = 30f, token = 30f, gap = 4f, pairGap = 14f, roadW = 34f;
            float width = hexes.Count * (icon + gap + token) + Mathf.Max(0, hexes.Count - 1) * pairGap + (road ? roadW + 6f : 0f);
            float x = -width / 2f;
            if (road)
            {
                var bar = CreateImage(button, "RoadGlyph", null);
                bar.color = new Color(0.45f, 0.28f, 0.16f, 1f);
                SetRect(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x + roadW / 2f, 0f), new Vector2(roadW, 9f));
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                bar.raycastTarget = false;
                x += roadW + 6f;
            }

            for (int h = 0; h < hexes.Count; h++)
            {
                if (!state.Board.TryGetTile(hexes[h], out var tile))
                    continue;
                var img = CreateImage(button, "SpotIcon_" + h, ColonistHudResourceSprites.Get(tile.Resource));
                img.preserveAspect = true;
                img.raycastTarget = false;
                SetRect(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x + icon / 2f, 0f), new Vector2(icon, icon));
                x += icon + gap;
                var disc = CreateDisc(button, "SpotToken_" + h, new Color(0.96f, 0.91f, 0.78f, 1f), token);
                disc.raycastTarget = false;
                SetRect(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x + token / 2f, 0f), new Vector2(token, token));
                bool hot = tile.NumberToken == 6 || tile.NumberToken == 8;
                var n = RunHudUiFactory.CreateText(disc.rectTransform, "N", tile.NumberToken.ToString(), 17,
                    TextAnchor.MiddleCenter, hot ? new Color(0.78f, 0.12f, 0.1f, 1f) : ColonistUiKit.Ink, FontStyle.Bold);
                var font = ColonistNumberTokenFont.Get();
                if (font != null)
                    n.font = font;
                n.raycastTarget = false;
                Stretch(n.rectTransform);
                x += token + pairGap;
            }
        }

        private static string RoadLabel(MatchState state, HexMath.Edge e)
        {
            string a = state.Board.TryGetTile(e.A.Hex, out var ta) ? $"{ResourceLabel(ta.Resource)} {ta.NumberToken}" : null;
            string b = state.Board.TryGetTile(e.B.Hex, out var tb) && e.B.Hex != e.A.Hex
                ? $"{ResourceLabel(tb.Resource)} {tb.NumberToken}" : null;
            return "Road by " + (a ?? b ?? "the coast") + (a != null && b != null && a != b ? " / " + b : "");
        }

        private void BuildRerollTab(RectTransform card, MatchState state, int seat)
        {
            var me = state.Seats[seat];
            HsText(card, "Kicker", $"TOKEN REROLL  ·  {me.Rerolls} left", 16, new Vector2(0.5f, 1f), new Vector2(0f, -112f),
                new Vector2(620f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            var sub = HsText(card, "Sub",
                me.Rerolls > 0
                    ? "Pick one of your tiles: its number is redrawn from the token pool (never a 6 or 8 next to another)."
                    : "Win a token reroll at the Night Market to redraw a number on one of your own tiles.",
                17, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(640f, 48f), ColonistUiKit.InkMuted, FontStyle.Italic);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (me.Rerolls <= 0)
                return;
            var tiles = _hotSeat.Match.RerollableTiles(seat).ToList();
            for (int i = 0; i < Mathf.Min(tiles.Count, 10); i++)
            {
                var hex = tiles[i];
                HsButton(card, "Reroll_" + i, TileLabel(state, hex) + "  ↻", new Vector2(0.5f, 1f),
                    new Vector2(-160f + (i % 2) * 320f, -230f - (i / 2) * 60f), new Vector2(300f, 50f), true, false,
                    () => _hotSeat.Apply(MatchCommand.Reroll(seat, hex)), 18);
            }
        }

        // ---------- seat strip marks ----------

        /// <summary>Bounty and face-down marks on a strip row (public: amounts and targets, never authors).</summary>
        private void BuildSeatShenaniganMarks(RectTransform row, MatchState state, int i)
        {
            if (!state.Config.PartyRules)
                return;
            int bounty = _hotSeat.Match.BountiesOn(i).Sum(b => b.Reward.Total);
            if (bounty > 0)
            {
                var tag = CreateDisc(row, "Bounty", new Color(0.62f, 0.16f, 0.1f, 1f), 32f);
                SetRect(tag.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 30f), new Vector2(32f, 32f));
                var n = RunHudUiFactory.CreateText(tag.rectTransform, "N", "$" + bounty, 14, TextAnchor.MiddleCenter,
                    ColonistUiKit.Cream, FontStyle.Bold);
                Stretch(n.rectTransform);
            }

            if (_hotSeat.Match.PendingSabotageOn(i) != null)
            {
                var back = CreateImage(row, "FaceDown", null);
                back.color = new Color(0.32f, 0.14f, 0.12f, 1f);
                SetRect(back.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, -26f), new Vector2(22f, 30f));
                var q = RunHudUiFactory.CreateText(back.rectTransform, "Q", "?", 16, TextAnchor.MiddleCenter,
                    new Color(1f, 0.85f, 0.4f, 1f), FontStyle.Bold);
                Stretch(q.rectTransform);
            }
        }
    }
}
