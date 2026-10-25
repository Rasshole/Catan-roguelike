using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Trade;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v135 pass-and-play panels: development cards (buy / play, Year of Plenty and Monopoly pickers), the trade
    /// board (bank trades at harbour rates, player offers with accept / decline / counter and a confirm per
    /// partner) and the manual discard on a 7. Everything only renders <see cref="HotSeatSession"/> state and
    /// forwards commands; local picker state lives here until a command is sent.
    /// </summary>
    public sealed partial class RunHudController
    {
        private bool _hsTradeOpen;
        private ResourceBundle _hsGive;
        private ResourceBundle _hsGet;
        private ResourceBundle _hsDiscard;
        private int _hsDiscardSeat = -1;
        private MatchDevCard? _hsPicker;
        private ResourceType? _hsYopFirst;
        private int _hsCounterSeat = -1;

        public bool HotSeatTradeOpenForTests => _hsTradeOpen;

        /// <summary>Tests/captures: open the trade board with these terms (as the acting player would with the steppers).</summary>
        public void SetHotSeatTradeForTests(bool open, ResourceBundle give, ResourceBundle get)
        {
            _hsTradeOpen = open;
            _hsGive = give;
            _hsGet = get;
            _hotSeatDirty = true;
        }

        /// <summary>Tests/captures: pre-select discards for the acting seat.</summary>
        public void SetHotSeatDiscardForTests(ResourceBundle discard)
        {
            _hsDiscardSeat = _hotSeat?.State.ActingSeat ?? -1;
            _hsDiscard = discard;
            _hotSeatDirty = true;
        }

        public static string ResourceLabel(ResourceType r) => r switch
        {
            ResourceType.Wood => "Lumber",
            ResourceType.Brick => "Brick",
            ResourceType.Wheat => "Grain",
            ResourceType.Sheep => "Wool",
            ResourceType.Stone => "Ore",
            _ => r.ToString()
        };

        public static string DevCardLabel(MatchDevCard c) => c switch
        {
            MatchDevCard.Knight => "Knight",
            MatchDevCard.VictoryPoint => "Victory Point",
            MatchDevCard.RoadBuilding => "Road Building",
            MatchDevCard.YearOfPlenty => "Year of Plenty",
            MatchDevCard.Monopoly => "Monopoly",
            _ => c.ToString()
        };

        private void HsMark() => _hotSeatDirty = true;

        private Button HsButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size,
            bool enabled, bool green, System.Action click, int fontSize = 18)
        {
            var b = RunHudUiFactory.CreateButton(parent, label, RunHudUiFactory.ButtonFill);
            b.name = name;
            SetRect((RectTransform)b.transform, anchor, pos, size);
            ColonistKenneyHudArt.ApplyColonistWarmButton(b.GetComponent<Image>(), primaryGreen: green);
            var text = b.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = green ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                text.fontSize = fontSize;
            }

            b.interactable = enabled;
            b.onClick.AddListener(() =>
            {
                click();
                HsMark();
            });
            return b;
        }

        private Text HsText(RectTransform parent, string name, string text, int size, Vector2 anchor, Vector2 pos,
            Vector2 box, Color color, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = RunHudUiFactory.CreateText(parent, name, text, size, align, color, style);
            SetRect(t.rectTransform, anchor, pos, box);
            return t;
        }

        private RectTransform HsModal(string name, Vector2 size, Vector2 offset)
        {
            var card = CreateCardFrame(_hotSeatRoot, name, wide: true);
            var img = card.GetComponent<Image>();
            var sliced = ColonistRoguelikeArt.GetCardFrameSliced(true);
            if (sliced != null)
            {
                img.sprite = sliced;
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
            }

            SetRect(card, new Vector2(0.5f, 0.5f), offset, size);
            return card;
        }

        private static readonly Vector2 HsModalOffset = new(-190f, 110f);

        // ---------- development cards ----------

        private void BuildHotSeatDevCards()
        {
            var s = _hotSeat;
            int seat = s.State.ActingSeat;
            if (!s.IsHandVisible(seat) || s.Match.IsSetup || HsTradeOnTable)
                return;
            var me = s.State.Seats[seat];
            var panel = CreateImage(_hotSeatRoot, "DevCards", null);
            panel.preserveAspect = false;
            ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
            SetRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(330f, 278f), new Vector2(580f, 132f));
            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            int hidden = me.HiddenVictoryPoints;
            HsText(panel.rectTransform, "Title",
                $"DEVELOPMENT CARDS  ·  {s.State.DevDeck.Count} left in deck" + (hidden > 0 ? $"  ·  +{hidden} hidden VP" : ""),
                15, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(540f, 20f), ink, FontStyle.Bold);

            var kinds = new[]
            {
                MatchDevCard.Knight, MatchDevCard.RoadBuilding, MatchDevCard.YearOfPlenty, MatchDevCard.Monopoly,
                MatchDevCard.VictoryPoint
            };
            var owned = kinds.Where(k => me.DevCards.Contains(k) || me.NewDevCards.Contains(k)).ToList();
            if (owned.Count == 0)
            {
                var cost = s.Match.DevCardCostFor(seat);
                var parts = MatchController.AllResources.Where(r => cost[r] > 0).Select(r => $"{cost[r]} {ResourceLabel(r).ToLowerInvariant()}");
                HsText(panel.rectTransform, "Empty", $"No cards yet: buy one for {string.Join(", ", parts)}.", 16,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(520f, 30f), ColonistUiKit.InkMuted,
                    FontStyle.Italic);
                return;
            }

            float w = Mathf.Min(170f, (540f - (owned.Count - 1) * 8f) / owned.Count);
            for (int i = 0; i < owned.Count; i++)
            {
                var kind = owned[i];
                int ready = me.DevCards.Count(c => c == kind);
                int fresh = me.NewDevCards.Count(c => c == kind);
                bool playable = kind != MatchDevCard.VictoryPoint && s.Match.CanPlayDevCard(seat, kind) && !s.PassScreenPending;
                string label = $"{DevCardLabel(kind)} ×{ready + fresh}" + (fresh > 0 && ready == 0 ? "\n(next turn)" : "");
                float x = -270f + w / 2f + i * (w + 8f);
                var k = kind;
                HsButton(panel.rectTransform, "Dev_" + kind, label, new Vector2(0.5f, 0.5f), new Vector2(x, -16f),
                    new Vector2(w, 64f), playable, playable, () => PlayDevCardFromHud(k), 15);
            }
        }

        private void PlayDevCardFromHud(MatchDevCard kind)
        {
            int seat = _hotSeat.State.ActingSeat;
            switch (kind)
            {
                case MatchDevCard.Knight:
                    _hotSeat.Apply(MatchCommand.Simple(MatchCommandType.PlayKnight, seat));
                    break;
                case MatchDevCard.RoadBuilding:
                    _hotSeat.Apply(MatchCommand.Simple(MatchCommandType.PlayRoadBuilding, seat));
                    break;
                case MatchDevCard.YearOfPlenty:
                case MatchDevCard.Monopoly:
                    _hsPicker = kind;
                    _hsYopFirst = null;
                    _hsTradeOpen = false;
                    break;
            }
        }

        private void BuildDevCardPicker()
        {
            if (!_hsPicker.HasValue)
                return;
            var kind = _hsPicker.Value;
            int seat = _hotSeat.State.ActingSeat;
            if (!_hotSeat.IsHandVisible(seat) || !_hotSeat.Match.CanPlayDevCard(seat, kind))
            {
                _hsPicker = null;
                return;
            }

            var card = HsModal("DevCardPicker", new Vector2(860f, 400f), HsModalOffset);
            string title = kind == MatchDevCard.Monopoly
                ? "Monopoly: everyone hands you all of one resource"
                : _hsYopFirst.HasValue
                    ? $"Year of Plenty: {ResourceLabel(_hsYopFirst.Value)} + pick the second card"
                    : "Year of Plenty: take any two cards from the bank";
            HsText(card, "Title", title, 22, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(700f, 34f),
                ColonistUiKit.Ink, FontStyle.Bold);
            var resources = MatchController.AllResources;
            for (int i = 0; i < resources.Length; i++)
            {
                var r = resources[i];
                float x = -280f + i * 140f;
                var icon = CreateImage(card, "Icon_" + r, ColonistHudResourceSprites.Get(r));
                SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 4f), new Vector2(64f, 64f));
                HsButton(card, "Pick_" + r, ResourceLabel(r), new Vector2(0.5f, 0.5f), new Vector2(x, -62f),
                    new Vector2(124f, 48f), true, true, () =>
                    {
                        if (kind == MatchDevCard.Monopoly)
                        {
                            _hsPicker = null;
                            _hotSeat.Apply(MatchCommand.Card(MatchCommandType.PlayMonopoly, seat, r, r));
                        }
                        else if (!_hsYopFirst.HasValue)
                            _hsYopFirst = r;
                        else
                        {
                            var first = _hsYopFirst.Value;
                            _hsPicker = null;
                            _hsYopFirst = null;
                            _hotSeat.Apply(MatchCommand.Card(MatchCommandType.PlayYearOfPlenty, seat, first, r));
                        }
                    });
            }

            HsButton(card, "PickerCancel", "Cancel", new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(200f, 48f),
                true, false, () =>
                {
                    _hsPicker = null;
                    _hsYopFirst = null;
                });
        }

        // ---------- discard on a 7 ----------

        private void BuildDiscardPicker()
        {
            var s = _hotSeat;
            var state = s.State;
            if (state.Phase != MatchPhase.Discard)
            {
                _hsDiscardSeat = -1;
                return;
            }

            int seat = state.ActingSeat;
            if (!s.IsHandVisible(seat) || !state.PendingDiscards.TryGetValue(seat, out int owed))
                return;
            if (_hsDiscardSeat != seat)
            {
                _hsDiscardSeat = seat;
                _hsDiscard = ResourceBundle.Zero;
            }

            var hand = state.Seats[seat].Hand;
            var card = HsModal("DiscardPicker", new Vector2(900f, 420f), HsModalOffset);
            HsText(card, "Kicker", "A SEVEN WAS ROLLED", 16, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(700f, 22f), ColonistUiKit.InkGold, FontStyle.Bold);
            HsText(card, "Title", $"{state.Seats[seat].Name}: discard {owed} of your {hand.Total} cards", 28,
                new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(800f, 40f), ColonistUiKit.Ink, FontStyle.Bold);
            var resources = MatchController.AllResources;
            for (int i = 0; i < resources.Length; i++)
            {
                var r = resources[i];
                float x = -300f + i * 150f;
                var icon = CreateImage(card, "Icon_" + r, ColonistHudResourceSprites.Get(r));
                SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 40f), new Vector2(64f, 64f));
                HsText(card, "Have_" + r, $"have {hand[r]}", 15, new Vector2(0.5f, 0.5f), new Vector2(x, -4f),
                    new Vector2(120f, 20f), ColonistUiKit.InkMuted);
                int picked = _hsDiscard[r];
                HsText(card, "Picked_" + r, picked > 0 ? $"−{picked}" : "0", 26, new Vector2(0.5f, 0.5f),
                    new Vector2(x, -36f), new Vector2(60f, 34f), picked > 0 ? TimerPulseRed : ColonistUiKit.Ink,
                    FontStyle.Bold);
                HsButton(card, "Less_" + r, "−", new Vector2(0.5f, 0.5f), new Vector2(x - 44f, -36f), new Vector2(40f, 38f),
                    picked > 0, false, () => _hsDiscard.Add(r, -1), 22);
                HsButton(card, "More_" + r, "+", new Vector2(0.5f, 0.5f), new Vector2(x + 44f, -36f), new Vector2(40f, 38f),
                    picked < hand[r] && _hsDiscard.Total < owed, false, () => _hsDiscard.Add(r, 1), 22);
            }

            HsButton(card, "DiscardSuggest", "Suggest", new Vector2(0.5f, 0f), new Vector2(-170f, 60f),
                new Vector2(200f, 54f), true, false, () => _hsDiscard = MatchController.SuggestDiscard(hand, owed));
            HsButton(card, "DiscardConfirm", $"Discard {_hsDiscard.Total}/{owed}", new Vector2(0.5f, 0f),
                new Vector2(90f, 60f), new Vector2(260f, 54f), _hsDiscard.Total == owed, true, () =>
                {
                    var pick = _hsDiscard;
                    _hsDiscard = ResourceBundle.Zero;
                    _hotSeat.Apply(MatchCommand.DiscardCards(seat, pick));
                });
        }

        // ---------- trade board (v138: icons + numbers; v139: leader portraits, "you get" on top, wildcards) ----------

        private int _hsTradeViewer = -1;
        private int _hsGiveAny;
        private int _hsGetAny;

        // v139 wildcard picker: the responder picks for the offerer's give wildcards when accepting
        // (_hsWildConfirm false), or the offerer picks for the get wildcards when confirming (true).
        private int _hsWildSeat = -1;
        private bool _hsWildConfirm;
        private ResourceBundle _hsWildPick;

        private const float HsTradeWidth = 740f;

        /// <summary>v139: the trade card sits over the empty left side of the table, clear of the board.</summary>
        private static readonly float HsTradeX = -(960f - 20f - HsTradeWidth / 2f);

        /// <summary>Tests/captures: look at the open offer as this seat (the responder holding the device).</summary>
        public void SetHotSeatTradeViewerForTests(int seat)
        {
            _hsTradeViewer = seat;
            HsMark();
        }

        /// <summary>Tests/captures: wildcard counts in the composer (as clicks on the wildcard card would).</summary>
        public void SetHotSeatTradeWildcardsForTests(int giveAny, int getAny)
        {
            _hsGiveAny = giveAny;
            _hsGetAny = getAny;
            HsMark();
        }

        public bool HotSeatWildPickerOpenForTests => _hsWildSeat >= 0;

        private void CloseWildPicker()
        {
            _hsWildSeat = -1;
            _hsWildConfirm = false;
            _hsWildPick = ResourceBundle.Zero;
        }

        /// <summary>v140: a trade card (composer or offer) is showing; it takes the dev-card panel's space.</summary>
        private bool HsTradeOnTable
        {
            get
            {
                var s = _hotSeat;
                if (s == null || s.IsOver || s.PassScreenPending)
                    return false;
                return s.State.ActiveTrade != null
                       || (_hsTradeOpen && s.State.Phase == MatchPhase.Main && !s.Acting.IsAi)
                       || HsSchemeOnTable; // v141: the scheme panel takes the same space
            }
        }

        private static float HsTradeOffsetY(float h) => 436f - h / 2f; // v140: top edge pinned (it covers the event card)

        private void BuildTradeBoard()
        {
            var s = _hotSeat;
            var state = s.State;
            var trade = state.ActiveTrade;
            if (trade == null)
                CloseWildPicker();
            if (trade == null && (!_hsTradeOpen || state.Phase != MatchPhase.Main || s.Acting.IsAi || s.PassScreenPending))
            {
                if (trade == null)
                {
                    _hsCounterSeat = -1;
                    _hsTradeViewer = -1;
                }

                return;
            }

            if (trade != null && (s.PassScreenPending || s.IsOver))
                return;

            int seat = state.ActingSeat;
            if (trade == null)
            {
                var composer = HsModal("TradeBoard", new Vector2(HsTradeWidth, 596f), new Vector2(HsTradeX, HsTradeOffsetY(596f)));
                HsText(composer, "Kicker", "TRADE", 16, new Vector2(0.5f, 1f), new Vector2(0f, -48f),
                    new Vector2(500f, 22f), TradeKickerInk, FontStyle.Bold);
                TradeButton(composer, "TradeClose", "", TradeGlyphSprites.Cross, new Vector2(1f, 1f),
                    TradeClosePos + new Vector2(20f, 0f), TradeCloseSize, true, false, () =>
                    {
                        _hsTradeOpen = false;
                        HsMark();
                    });
                BuildTradeComposer(composer, seat);
                return;
            }

            BuildTradeOffer(trade, seat);
        }

        /// <summary>v139: how many give wildcards the hand can back (cards outside the offer and the request).</summary>
        private static int GiveWildLimit(ResourceBundle hand, ResourceBundle give, ResourceBundle get) =>
            Mathf.Min(TradeWildcards.MaxWildcards, MatchController.OffererWildPool(hand, give, get).Total);

        private void BuildTradeComposer(RectTransform card, int seat)
        {
            var s = _hotSeat;
            var me = s.State.Seats[seat];
            // Keep the wildcards legal as the rows change (fewer spare cards → fewer wildcards).
            _hsGiveAny = Mathf.Min(_hsGiveAny, GiveWildLimit(me.Hand, _hsGive, _hsGet));
            // v139: who trades — your leader, then the leaders your offer goes to.
            var anchor = new Vector2(0f, 1f);
            HsTradePortrait(card, "ComposeFrom", seat, anchor, new Vector2(116f, -106f), 58f);
            HsText(card, "ComposeName", me.Name, 21, anchor, new Vector2(156f + 80f, -106f), new Vector2(160f, 30f),
                ColonistUiKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            TradeGlyph(card, "ComposeTo", TradeGlyphSprites.Counter, anchor, new Vector2(334f, -106f), 34f);
            int k = 0;
            for (int j = 0; j < s.State.Seats.Count; j++)
                if (j != seat)
                    HsTradePortrait(card, "ComposeTo_" + j, j, anchor, new Vector2(388f + k++ * 56f, -106f), 48f);
            BuildTradeRateBadges(card, -160f, r => s.Match.BankRatio(seat, r));
            float x0 = TradeRowX0(HsTradeWidth);
            // v139: what you get on top (green, toward you), what you give below (red, away from you).
            BuildTradeEditRow(card, "Get", false, -250f, x0, () => _hsGet, b => _hsGet = b, Cap(9), HsMark,
                new TradeWildSlot { Get = () => _hsGetAny, Set = n => _hsGetAny = n, Limit = _hsGiveAny > 0 ? 0 : TradeWildcards.MaxWildcards });
            BuildTradeEditRow(card, "Give", true, -376f, x0, () => _hsGive, b => _hsGive = b, me.Hand, HsMark,
                new TradeWildSlot
                {
                    Get = () => _hsGiveAny, Set = n => _hsGiveAny = n,
                    Limit = _hsGetAny > 0 ? 0 : GiveWildLimit(me.Hand, _hsGive, _hsGet)
                });

            // v143: multi-batch bank trades — e.g. 4 Wool → 2 at a 2:1 harbour, 8 → 2 at 4:1, or 2 Wool (2:1) + 3 Ore (3:1)
            // for two different cards. Every give-resource must be whole batches at its own rate.
            int batchCards = _hsGive.Total > 0 ? s.Match.BankBatchCards(seat, _hsGive) : -1;
            bool disjoint = !MatchController.AllResources.Any(r => _hsGive[r] > 0 && _hsGet[r] > 0);
            bool bankOk = _hsGiveAny == 0 && _hsGetAny == 0 && batchCards > 0 && batchCards == _hsGet.Total && disjoint
                          && me.Hand.CanAfford(_hsGive);
            int shownRate = TradeResources.Any(r => _hsGive[r] > 0)
                ? s.Match.BankRatio(seat, TradeResources.First(r => _hsGive[r] > 0))
                : TradeResources.Min(r => s.Match.BankRatio(seat, r));
            string bankLabel = bankOk && (_hsGet.Total > 1 || TradeResources.Count(r => _hsGive[r] > 0) > 1)
                ? $"Bank {_hsGive.Total}→{_hsGet.Total}"
                : $"Bank {shownRate}:1";
            TradeButton(card, "BankTrade", bankLabel, null, new Vector2(0.5f, 0f), new Vector2(-186f, 58f),
                new Vector2(176f, 52f), bankOk, false, () =>
                {
                    var r = s.Apply(MatchCommand.BankBatch(seat, _hsGive, _hsGet));
                    if (r.Ok)
                    {
                        _hsGive = ResourceBundle.Zero;
                        _hsGet = ResourceBundle.Zero;
                    }

                    HsMark();
                }, 19);
            bool offerOk = _hsGive.Total + _hsGiveAny > 0 && _hsGet.Total + _hsGetAny > 0 && me.Hand.CanAfford(_hsGive)
                           && !(_hsGiveAny > 0 && _hsGetAny > 0)
                           && !MatchController.AllResources.Any(r => _hsGive[r] > 0 && _hsGet[r] > 0)
                           && _hsGiveAny <= GiveWildLimit(me.Hand, _hsGive, _hsGet);
            TradeButton(card, "ProposeTrade", "Offer", TradeGlyphSprites.Counter, new Vector2(0.5f, 0f),
                new Vector2(20f, 58f), new Vector2(196f, 52f), offerOk, true, () =>
                {
                    s.Apply(MatchCommand.Propose(seat, _hsGive, _hsGet, _hsGiveAny, _hsGetAny));
                    HsMark();
                }, 19);
            TradeButton(card, "TradeClear", "", TradeGlyphSprites.Cross, new Vector2(0.5f, 0f), new Vector2(172f, 58f),
                new Vector2(62f, 52f), _hsGive.Total + _hsGet.Total + _hsGiveAny + _hsGetAny > 0, false, () =>
                {
                    _hsGive = ResourceBundle.Zero;
                    _hsGet = ResourceBundle.Zero;
                    _hsGiveAny = _hsGetAny = 0;
                    HsMark();
                });
        }

        private static ResourceBundle Cap(int n) => new() { Wood = n, Brick = n, Wheat = n, Sheep = n, Stone = n };

        /// <summary>v139: a seat's leader in a seat-coloured ring (coloured disc + number / AI without party civs).</summary>
        private Image HsTradePortrait(RectTransform parent, string name, int seat, Vector2 anchor, Vector2 pos, float size)
        {
            var state = _hotSeat.State;
            var st = state.Seats[seat];
            bool leader = state.Config.PartyRules && SeatCivRevealed(state, st);
            var img = BuildTradePortrait(parent, name, PlayerPieceVisuals.ColorForSeat(seat),
                leader ? ColonistRoguelikeArt.GetCivilizationPortrait(st.Civilization) : null,
                st.IsAi ? "AI" : (seat + 1).ToString(), SeatInk(seat), anchor, pos, size);
            return img;
        }

        /// <summary>
        /// The open offer. Terms are drawn from the VIEWER's side: the offerer by default, or the responder holding the
        /// device (click a responder's portrait). v139: what the viewer gets (green) on top, what they give (red) below.
        /// </summary>
        private void BuildTradeOffer(MatchTradeOffer trade, int seat)
        {
            var state = _hotSeat.State;
            var match = _hotSeat.Match;
            var from = state.Seats[trade.From];
            if (_hsTradeViewer < 0 || _hsTradeViewer >= state.Seats.Count || state.Seats[_hsTradeViewer].IsAi)
                _hsTradeViewer = DefaultTradeViewer(trade);
            int viewer = _hsTradeViewer;
            bool offererView = viewer == trade.From;
            var viewerResp = offererView ? null : trade.Responses[viewer];
            bool viewerCanAnswer = viewerResp != null && viewerResp.Kind == TradeResponseKind.Pending;
            bool counterOpen = _hsCounterSeat >= 0 && _hsCounterSeat != trade.From;
            bool pickerOpen = _hsWildSeat >= 0 && !counterOpen;

            // v140: while a counter or the wildcard picker is open only that player's row stays, so the card
            // never runs into the hand panel (the dev-card panel steps aside while a trade is on the table).
            int focus = counterOpen ? _hsCounterSeat : pickerOpen ? _hsWildSeat : -1;
            bool compact = focus >= 0;
            int rows = compact ? (focus == trade.From ? 0 : 1) : state.Seats.Count - 1;
            const float rowH = 68f;
            const float headH = 222f;
            float counterH = counterOpen ? 316f : 0f;
            float pickerH = pickerOpen ? TradePickerHeight + 12f : 0f;
            float h = headH + rows * rowH + counterH + pickerH + (offererView ? 116f : 66f);
            var card = HsModal("TradeBoard", new Vector2(HsTradeWidth, h), new Vector2(HsTradeX, HsTradeOffsetY(h)));
            HsText(card, "Kicker", offererView ? "YOUR OFFER" : $"INCOMING OFFER  ·  {state.Seats[viewer].Name.ToUpperInvariant()} LOOKS",
                16, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(560f, 22f), TradeKickerInk,
                FontStyle.Bold);

            // Header: who offers (leader portrait) + the terms from the viewer's side, "you get" above "you give".
            HsTradePortrait(card, "FromDisc", trade.From, new Vector2(0f, 1f), new Vector2(118f, -146f), 76f);
            HsText(card, "FromName", offererView ? from.Name : $"{from.Name} → {state.Seats[viewer].Name}", 22,
                new Vector2(0f, 1f), new Vector2(178f + 150f, -100f), new Vector2(300f, 30f), ColonistUiKit.Ink,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var youGet = offererView ? trade.Get : trade.Give;
            int youGetAny = offererView ? trade.GetAny : trade.GiveAny;
            var youGive = offererView ? trade.Give : trade.Get;
            int youGiveAny = offererView ? trade.GiveAny : trade.GetAny;
            BuildTradeTerms(card, "HeadGet", false, youGet, new Vector2(0f, 1f), 178f, -144f, 40f, youGetAny);
            BuildTradeTerms(card, "HeadGive", true, youGive, new Vector2(0f, 1f), 178f, -190f, 40f, youGiveAny);
            if (!offererView && viewerCanAnswer && !pickerOpen)
            {
                int v = viewer;
                TradeButton(card, "Accept_" + v, "", TradeGlyphSprites.CheckCream, new Vector2(1f, 1f),
                    new Vector2(-232f, -166f), new Vector2(64f, 56f), CanAcceptHs(v, trade), true,
                    () => Answer(v, TradeResponseKind.Accepted));
                TradeButton(card, "Decline_" + v, "", TradeGlyphSprites.Cross, new Vector2(1f, 1f),
                    new Vector2(-160f, -166f), new Vector2(64f, 56f), true, false,
                    () => Answer(v, TradeResponseKind.Declined));
                TradeButton(card, "Counter_" + v, "", TradeGlyphSprites.Counter, new Vector2(1f, 1f),
                    new Vector2(-88f, -166f), new Vector2(64f, 56f), true, false, () => OpenCounter(v, trade));
            }

            var rule = CreateImage(card, "HeadRule", null);
            rule.color = ColonistUiKit.Rule;
            SetRect(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -headH), new Vector2(620f, 2f));

            int row = 0;
            for (int i = 0; i < state.Seats.Count; i++)
            {
                if (i == trade.From || (compact && i != focus))
                    continue;
                BuildTradeResponseRow(card, trade, i, viewer, -headH - rowH / 2f - row++ * rowH);
            }

            float below = -headH - rows * rowH - 10f;
            if (counterOpen)
                BuildCounterEditor(card, trade, below - 6f);
            else if (pickerOpen)
                BuildHsWildPicker(card, trade, below);

            if (offererView)
                TradeButton(card, "CancelOffer", "Withdraw", TradeGlyphSprites.Cross, new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(206f, 50f), !from.IsAi && offererView, false, () =>
                {
                    _hsCounterSeat = -1;
                    _hsTradeViewer = -1;
                    CloseWildPicker();
                    _hotSeat.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, trade.From));
                    HsMark();
                }, 18);
        }

        /// <summary>
        /// v142: who looks at an offer by default — the offerer, unless an AI made it; then the first human who still
        /// has to answer (or any human), so the card reads "INCOMING OFFER" with Accept / Decline / Counter.
        /// </summary>
        private int DefaultTradeViewer(MatchTradeOffer trade)
        {
            var seats = _hotSeat.State.Seats;
            if (!seats[trade.From].IsAi)
                return trade.From;
            for (int i = 0; i < seats.Count; i++)
                if (i != trade.From && !seats[i].IsAi && trade.Responses[i].Kind == TradeResponseKind.Pending)
                    return i;
            for (int i = 0; i < seats.Count; i++)
                if (i != trade.From && !seats[i].IsAi)
                    return i;
            return trade.From;
        }

        /// <summary>v139: whether a responder could accept (cards for the request + any wildcards backed).</summary>
        private bool CanAcceptHs(int seat, MatchTradeOffer trade)
        {
            var match = _hotSeat.Match;
            var hand = match.State.Seats[seat].Hand;
            if (!hand.CanAfford(trade.Get))
                return false;
            if (trade.GiveAny > 0 && match.AcceptWildPool(trade).Total < trade.GiveAny)
                return false;
            return trade.GetAny == 0 || MatchController.PartnerWildPool(hand, trade, ResourceBundle.Zero).Total >= trade.GetAny;
        }

        private void BuildHsWildPicker(RectTransform card, MatchTradeOffer trade, float top)
        {
            var state = _hotSeat.State;
            int seat = _hsWildSeat;
            bool confirm = _hsWildConfirm;
            int n = confirm ? trade.GetAny : trade.GiveAny;
            var pool = confirm
                ? MatchController.PartnerWildPool(state.Seats[seat].Hand, trade, trade.Responses[seat].AnyChoice)
                : _hotSeat.Match.AcceptWildPool(trade);
            string title = confirm
                ? $"{state.Seats[trade.From].Name}: pick {n} from {state.Seats[seat].Name}"
                : $"{state.Seats[seat].Name}: pick {n} from {state.Seats[trade.From].Name}";
            BuildTradeWildPicker(card, title, top, HsTradeWidth, n, pool, () => _hsWildPick, b => _hsWildPick = b, HsMark,
                () =>
                {
                    var cmd = confirm
                        ? MatchCommand.Confirm(trade.From, seat, _hsWildPick)
                        : MatchCommand.AcceptWithChoice(seat, _hsWildPick);
                    var r = _hotSeat.Apply(cmd);
                    if (r.Ok)
                    {
                        CloseWildPicker();
                        _hsTradeViewer = confirm ? -1 : trade.From;
                    }

                    HsMark();
                },
                () =>
                {
                    CloseWildPicker();
                    HsMark();
                });
        }

        private void Answer(int seat, TradeResponseKind kind)
        {
            var trade = _hotSeat.State.ActiveTrade;
            if (kind == TradeResponseKind.Accepted && trade != null && trade.GiveAny > 0)
            {
                // v139: "any resource you choose" — the accepter picks first.
                _hsWildSeat = seat;
                _hsWildConfirm = false;
                _hsWildPick = ResourceBundle.Zero;
                _hsTradeViewer = seat;
                _hsCounterSeat = -1;
                HsMark();
                return;
            }

            _hotSeat.Apply(MatchCommand.Respond(seat, kind));
            HsMark();
        }

        private void OpenCounter(int seat, MatchTradeOffer trade)
        {
            _hsCounterSeat = seat;
            _hsTradeViewer = seat;
            CloseWildPicker();
            // Counters carry plain cards only: the wildcards drop out.
            _hsGive = trade.Give;
            _hsGet = trade.Get;
            HsMark();
        }

        private void BuildTradeResponseRow(RectTransform card, MatchTradeOffer trade, int i, int viewer, float y)
        {
            var state = _hotSeat.State;
            var other = state.Seats[i];
            var response = trade.Responses[i];
            bool isViewer = i == viewer;
            if (isViewer)
            {
                var hl = CreateImage(card, "TradeRowHl_" + i, null);
                hl.color = new Color(1f, 0.82f, 0.36f, 0.35f);
                SetRect(hl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(612f, 62f));
            }

            // The portrait is the "hand me the device" switch for human responders.
            var ring = HsTradePortrait(card, "TradeDisc_" + i, i, new Vector2(0f, 1f), new Vector2(118f, y), 56f);
            MakeTradePortraitButton(ring, !other.IsAi, () =>
            {
                _hsTradeViewer = _hsTradeViewer == i ? trade.From : i;
                _hsCounterSeat = -1;
                if (!_hsWildConfirm)
                    CloseWildPicker();
                HsMark();
            });
            HsText(card, "TradeName_" + i, other.Name, 20, new Vector2(0f, 1f), new Vector2(160f + 70f, y),
                new Vector2(140f, 30f), ColonistUiKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);

            float statusX = 326f;
            switch (response.Kind)
            {
                case TradeResponseKind.Accepted:
                    TradeStatusMark(card, "TradeStatus_" + i, TradeGlyphSprites.Check, new Vector2(0f, 1f),
                        new Vector2(statusX, y), 42f);
                    if (response.AnyChoice.Total > 0 && (viewer == i || viewer == trade.From))
                    {
                        // v139: only the two trade partners see the pick. What the accepter picked for the wildcards (they get it: red for the offerer).
                        bool responderView = viewer == i;
                        BuildTradeTerms(card, "Picked_" + i, !responderView, response.AnyChoice, new Vector2(0f, 1f),
                            statusX + 32f, y, 34f);
                    }

                    break;
                case TradeResponseKind.Declined:
                    TradeStatusMark(card, "TradeStatus_" + i, TradeGlyphSprites.Cross, new Vector2(0f, 1f),
                        new Vector2(statusX, y), 42f);
                    break;
                case TradeResponseKind.Countered:
                {
                    TradeStatusMark(card, "TradeStatus_" + i, TradeGlyphSprites.Counter, new Vector2(0f, 1f),
                        new Vector2(statusX, y), 42f);
                    // Counter terms from the viewer's side (stored from the offerer's side); what you get first.
                    bool responderView = viewer == i;
                    var give = responderView ? response.CounterGet : response.CounterGive;
                    var get = responderView ? response.CounterGive : response.CounterGet;
                    float x = statusX + 32f;
                    x += BuildTradeTerms(card, "CounterGet_" + i, false, get, new Vector2(0f, 1f), x, y, 34f) + 8f;
                    BuildTradeTerms(card, "CounterGive_" + i, true, give, new Vector2(0f, 1f), x, y, 34f);
                    break;
                }
                default:
                {
                    var dots = RunHudUiFactory.CreateText(card, "TradeStatus_" + i, "…", 30, TextAnchor.MiddleCenter,
                        ColonistUiKit.InkMuted, FontStyle.Bold);
                    SetRect(dots.rectTransform, new Vector2(0f, 1f), new Vector2(statusX, y + 6f), new Vector2(44f, 44f));
                    break;
                }
            }

            bool canAnswer = !other.IsAi && response.Kind == TradeResponseKind.Pending;
            bool offererView = viewer == trade.From;
            if (canAnswer && offererView)
            {
                int responder = i;
                TradeButton(card, "Accept_" + i, "", TradeGlyphSprites.CheckCream, new Vector2(1f, 1f),
                    new Vector2(-222f, y), new Vector2(58f, 48f), CanAcceptHs(i, trade), true,
                    () => Answer(responder, TradeResponseKind.Accepted));
                TradeButton(card, "Decline_" + i, "", TradeGlyphSprites.Cross, new Vector2(1f, 1f),
                    new Vector2(-158f, y), new Vector2(58f, 48f), true, false,
                    () => Answer(responder, TradeResponseKind.Declined));
                TradeButton(card, "Counter_" + i, "", TradeGlyphSprites.Counter, new Vector2(1f, 1f),
                    new Vector2(-94f, y), new Vector2(58f, 48f), true, false, () => OpenCounter(responder, trade));
            }
            else if (offererView && (response.Kind == TradeResponseKind.Accepted || response.Kind == TradeResponseKind.Countered))
            {
                int partner = i;
                TradeButton(card, "Confirm_" + i, "Trade", TradeGlyphSprites.CheckCream, new Vector2(1f, 1f),
                    new Vector2(-118f, y), new Vector2(124f, 48f), !state.Seats[trade.From].IsAi && offererView, true,
                    () =>
                    {
                        if (response.Kind == TradeResponseKind.Accepted && trade.GetAny > 0)
                        {
                            // v139: "give me N of anything" — the offerer picks from what the partner can spare.
                            _hsWildSeat = partner;
                            _hsWildConfirm = true;
                            _hsWildPick = ResourceBundle.Zero;
                            _hsCounterSeat = -1;
                            HsMark();
                            return;
                        }

                        _hsTradeViewer = -1;
                        _hotSeat.Apply(MatchCommand.Confirm(trade.From, partner));
                        HsMark();
                    }, 18);
            }
        }

        /// <summary>Counter-offer editor, from the responder's side (stored back from the offerer's side).</summary>
        private void BuildCounterEditor(RectTransform card, MatchTradeOffer trade, float top)
        {
            var state = _hotSeat.State;
            int counterSeat = _hsCounterSeat;
            HsText(card, "CounterTitle", $"{state.Seats[counterSeat].Name}: your counter", 18, new Vector2(0.5f, 1f),
                new Vector2(0f, top - 14f), new Vector2(600f, 24f), ColonistUiKit.InkGold, FontStyle.Bold);
            float x0 = TradeRowX0(HsTradeWidth, false);
            // v139: what the responder gets (= what the offerer gives) on top, what they give below.
            BuildTradeEditRow(card, "CYouGet", false, top - 86f, x0, () => _hsGive, b => _hsGive = b, Cap(9), HsMark);
            BuildTradeEditRow(card, "CYouGive", true, top - 214f, x0, () => _hsGet, b => _hsGet = b,
                state.Seats[counterSeat].Hand, HsMark);
            TradeButton(card, "SendCounter", "Send", TradeGlyphSprites.Counter, new Vector2(1f, 1f),
                new Vector2(-150f, top - 284f), new Vector2(170f, 50f), _hsGive.Total > 0 && _hsGet.Total > 0, true, () =>
                {
                    _hotSeat.Apply(MatchCommand.Respond(counterSeat, TradeResponseKind.Countered, _hsGive, _hsGet));
                    _hsCounterSeat = -1;
                    _hsTradeViewer = trade.From;
                    HsMark();
                }, 18);
        }
    }
}
