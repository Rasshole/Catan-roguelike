using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Match;
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
            if (!s.IsHandVisible(seat) || s.Match.IsSetup)
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
                HsText(panel.rectTransform, "Empty", "No cards yet: buy one for 1 grain, 1 wool, 1 ore.", 16,
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

        // ---------- trade board (v138: icons + numbers, red give / green get arrows) ----------

        private int _hsTradeViewer = -1;

        /// <summary>Tests/captures: look at the open offer as this seat (the responder holding the device).</summary>
        public void SetHotSeatTradeViewerForTests(int seat)
        {
            _hsTradeViewer = seat;
            HsMark();
        }

        private string Terms(ResourceBundle b) =>
            b.Total == 0
                ? "nothing"
                : string.Join(" + ", MatchController.AllResources.Where(r => b[r] > 0).Select(r => $"{b[r]} {ResourceLabel(r)}"));

        private static float HsTradeOffsetY(float h) => Mathf.Min(110f, 436f - h / 2f);

        private void BuildTradeBoard()
        {
            var s = _hotSeat;
            var state = s.State;
            var trade = state.ActiveTrade;
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
                var composer = HsModal("TradeBoard", new Vector2(780f, 560f), new Vector2(-190f, HsTradeOffsetY(560f)));
                HsText(composer, "Kicker", "TRADE", 16, new Vector2(0.5f, 1f), new Vector2(0f, -48f),
                    new Vector2(500f, 22f), TradeKickerInk, FontStyle.Bold);
                TradeButton(composer, "TradeClose", "", TradeGlyphSprites.Cross, new Vector2(1f, 1f),
                    TradeClosePos, TradeCloseSize, true, false, () =>
                    {
                        _hsTradeOpen = false;
                        HsMark();
                    });
                BuildTradeComposer(composer, seat);
                return;
            }

            BuildTradeOffer(trade, seat);
        }

        private void BuildTradeComposer(RectTransform card, int seat)
        {
            var s = _hotSeat;
            var me = s.State.Seats[seat];
            BuildTradeRateBadges(card, -96f, r => s.Match.BankRatio(seat, r));
            const float x0 = 142f;
            BuildTradeEditRow(card, "Give", true, -196f, x0, () => _hsGive, b => _hsGive = b, me.Hand, HsMark);
            BuildTradeEditRow(card, "Get", false, -340f, x0, () => _hsGet, b => _hsGet = b, Cap(9), HsMark);

            bool bankOk = TryReadBankTrade(_hsGive, _hsGet, r => s.Match.BankRatio(seat, r), out var giveType,
                              out var getType, out int amount) && amount == 1 && me.Hand.CanAfford(_hsGive);
            int shownRate = TradeResources.Any(r => _hsGive[r] > 0)
                ? s.Match.BankRatio(seat, TradeResources.First(r => _hsGive[r] > 0))
                : TradeResources.Min(r => s.Match.BankRatio(seat, r));
            TradeButton(card, "BankTrade", $"Bank {shownRate}:1", null, new Vector2(0.5f, 0f), new Vector2(-196f, 62f),
                new Vector2(180f, 54f), bankOk, false, () =>
                {
                    var r = s.Apply(MatchCommand.Trade(seat, giveType, getType));
                    if (r.Ok)
                    {
                        _hsGive = ResourceBundle.Zero;
                        _hsGet = ResourceBundle.Zero;
                    }

                    HsMark();
                }, 19);
            bool offerOk = _hsGive.Total > 0 && _hsGet.Total > 0 && me.Hand.CanAfford(_hsGive)
                           && !MatchController.AllResources.Any(r => _hsGive[r] > 0 && _hsGet[r] > 0);
            TradeButton(card, "ProposeTrade", "Offer", TradeGlyphSprites.Counter, new Vector2(0.5f, 0f),
                new Vector2(20f, 62f), new Vector2(200f, 54f), offerOk, true, () =>
                {
                    s.Apply(MatchCommand.Propose(seat, _hsGive, _hsGet));
                    HsMark();
                }, 19);
            TradeButton(card, "TradeClear", "", TradeGlyphSprites.Cross, new Vector2(0.5f, 0f), new Vector2(178f, 62f),
                new Vector2(64f, 54f), _hsGive.Total + _hsGet.Total > 0, false, () =>
                {
                    _hsGive = ResourceBundle.Zero;
                    _hsGet = ResourceBundle.Zero;
                    HsMark();
                });
        }

        private static ResourceBundle Cap(int n) => new() { Wood = n, Brick = n, Wheat = n, Sheep = n, Stone = n };

        /// <summary>
        /// The open offer. Terms are drawn from the VIEWER's side: the offerer by default, or the responder holding the
        /// device (click a responder's disc). What the viewer gives is red, what the viewer gets is green.
        /// </summary>
        private void BuildTradeOffer(MatchTradeOffer trade, int seat)
        {
            var state = _hotSeat.State;
            var from = state.Seats[trade.From];
            if (_hsTradeViewer < 0 || _hsTradeViewer >= state.Seats.Count || state.Seats[_hsTradeViewer].IsAi)
                _hsTradeViewer = trade.From;
            int viewer = _hsTradeViewer;
            bool offererView = viewer == trade.From;
            var viewerResp = offererView ? null : trade.Responses[viewer];
            bool viewerCanAnswer = viewerResp != null && viewerResp.Kind == TradeResponseKind.Pending;
            bool counterOpen = _hsCounterSeat >= 0 && _hsCounterSeat != trade.From;

            int rows = state.Seats.Count - 1;
            const float rowH = 70f;
            float headH = 196f;
            float counterH = counterOpen ? 330f : 0f;
            float h = headH + rows * rowH + counterH + (offererView ? 120f : 70f);
            var card = HsModal("TradeBoard", new Vector2(780f, h), new Vector2(-190f, HsTradeOffsetY(h)));
            HsText(card, "Kicker", offererView ? "YOUR OFFER" : $"INCOMING OFFER  ·  {state.Seats[viewer].Name.ToUpperInvariant()} LOOKS",
                16, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(560f, 22f), TradeKickerInk,
                FontStyle.Bold);

            // Header: who offers + the terms from the viewer's side.
            var disc = CreateDisc(card, "FromDisc", PlayerPieceVisuals.ColorForSeat(trade.From), 50f);
            SetRect(disc.rectTransform, new Vector2(0f, 1f), new Vector2(110f, -104f), new Vector2(50f, 50f));
            HsText(card, "FromName", offererView ? from.Name : $"{from.Name} → {state.Seats[viewer].Name}", 22,
                new Vector2(0f, 1f), new Vector2(256f, -104f), new Vector2(230f, 30f), ColonistUiKit.Ink, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            var youGive = offererView ? trade.Give : trade.Get;
            var youGet = offererView ? trade.Get : trade.Give;
            BuildTradeTerms(card, "HeadGive", true, youGive, new Vector2(0f, 1f), 92f, -158f, 46f);
            float gw = TradeTermsWidth(youGive, 46f);
            BuildTradeTerms(card, "HeadGet", false, youGet, new Vector2(0f, 1f), 92f + gw + 34f, -158f, 46f);
            if (!offererView && viewerCanAnswer)
            {
                int v = viewer;
                TradeButton(card, "Accept_" + v, "", TradeGlyphSprites.CheckCream, new Vector2(1f, 1f),
                    new Vector2(-232f, -130f), new Vector2(64f, 56f), state.Seats[v].Hand.CanAfford(trade.Get), true,
                    () => Answer(v, TradeResponseKind.Accepted));
                TradeButton(card, "Decline_" + v, "", TradeGlyphSprites.Cross, new Vector2(1f, 1f),
                    new Vector2(-160f, -130f), new Vector2(64f, 56f), true, false,
                    () => Answer(v, TradeResponseKind.Declined));
                TradeButton(card, "Counter_" + v, "", TradeGlyphSprites.Counter, new Vector2(1f, 1f),
                    new Vector2(-88f, -130f), new Vector2(64f, 56f), true, false, () => OpenCounter(v, trade));
            }

            var rule = CreateImage(card, "HeadRule", null);
            rule.color = ColonistUiKit.Rule;
            SetRect(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(640f, 2f));

            int row = 0;
            for (int i = 0; i < state.Seats.Count; i++)
            {
                if (i == trade.From)
                    continue;
                BuildTradeResponseRow(card, trade, i, viewer, -196f - rowH / 2f - row++ * rowH);
            }

            if (counterOpen)
                BuildCounterEditor(card, trade, -196f - rows * rowH - 16f);

            if (offererView)
                TradeButton(card, "CancelOffer", "Withdraw", TradeGlyphSprites.Cross, new Vector2(0.5f, 0f),
                new Vector2(0f, 62f), new Vector2(210f, 52f), !from.IsAi && offererView, false, () =>
                {
                    _hsCounterSeat = -1;
                    _hsTradeViewer = -1;
                    _hotSeat.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, trade.From));
                    HsMark();
                }, 18);
        }

        private void Answer(int seat, TradeResponseKind kind)
        {
            _hotSeat.Apply(MatchCommand.Respond(seat, kind));
            HsMark();
        }

        private void OpenCounter(int seat, MatchTradeOffer trade)
        {
            _hsCounterSeat = seat;
            _hsTradeViewer = seat;
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
                SetRect(hl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(630f, 62f));
            }

            // The disc is the "hand me the device" switch for human responders.
            var discBtn = TradeButton(card, "TradeDisc_" + i, "", null, new Vector2(0f, 1f), new Vector2(110f, y),
                new Vector2(50f, 50f), !other.IsAi, false, () =>
                {
                    _hsTradeViewer = _hsTradeViewer == i ? trade.From : i;
                    _hsCounterSeat = -1;
                    HsMark();
                });
            var discColors = discBtn.colors;
            discColors.disabledColor = Color.white;
            discBtn.colors = discColors;
            var discImg = discBtn.GetComponent<Image>();
            discImg.sprite = DiscSprite();
            discImg.type = Image.Type.Simple;
            discImg.color = PlayerPieceVisuals.ColorForSeat(i);
            var tag = discBtn.GetComponentInChildren<Text>();
            tag.text = other.IsAi ? "AI" : (i + 1).ToString();
            tag.color = SeatInk(i);
            tag.fontSize = other.IsAi ? 15 : 20;
            HsText(card, "TradeName_" + i, other.Name, 20, new Vector2(0f, 1f), new Vector2(222f, y),
                new Vector2(150f, 30f), ColonistUiKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);

            float statusX = 330f;
            switch (response.Kind)
            {
                case TradeResponseKind.Accepted:
                    TradeStatusMark(card, "TradeStatus_" + i, TradeGlyphSprites.Check, new Vector2(0f, 1f),
                        new Vector2(statusX, y), 44f);
                    break;
                case TradeResponseKind.Declined:
                    TradeStatusMark(card, "TradeStatus_" + i, TradeGlyphSprites.Cross, new Vector2(0f, 1f),
                        new Vector2(statusX, y), 44f);
                    break;
                case TradeResponseKind.Countered:
                {
                    TradeStatusMark(card, "TradeStatus_" + i, TradeGlyphSprites.Counter, new Vector2(0f, 1f),
                        new Vector2(statusX, y), 44f);
                    // Counter terms from the viewer's side (stored from the offerer's side).
                    bool responderView = viewer == i;
                    var give = responderView ? response.CounterGet : response.CounterGive;
                    var get = responderView ? response.CounterGive : response.CounterGet;
                    float x = statusX + 32f;
                    x += BuildTradeTerms(card, "CounterGive_" + i, true, give, new Vector2(0f, 1f), x, y, 38f) + 10f;
                    BuildTradeTerms(card, "CounterGet_" + i, false, get, new Vector2(0f, 1f), x, y, 38f);
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
                    new Vector2(-232f, y), new Vector2(60f, 50f), other.Hand.CanAfford(trade.Get), true,
                    () => Answer(responder, TradeResponseKind.Accepted));
                TradeButton(card, "Decline_" + i, "", TradeGlyphSprites.Cross, new Vector2(1f, 1f),
                    new Vector2(-164f, y), new Vector2(60f, 50f), true, false,
                    () => Answer(responder, TradeResponseKind.Declined));
                TradeButton(card, "Counter_" + i, "", TradeGlyphSprites.Counter, new Vector2(1f, 1f),
                    new Vector2(-96f, y), new Vector2(60f, 50f), true, false, () => OpenCounter(responder, trade));
            }
            else if (offererView && (response.Kind == TradeResponseKind.Accepted || response.Kind == TradeResponseKind.Countered))
            {
                int partner = i;
                TradeButton(card, "Confirm_" + i, "Trade", TradeGlyphSprites.CheckCream, new Vector2(1f, 1f),
                    new Vector2(-130f, y), new Vector2(150f, 50f), !state.Seats[trade.From].IsAi && offererView, true,
                    () =>
                    {
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
            const float x0 = 142f;
            // The responder gives what the offerer gets, and gets what the offerer gives.
            BuildTradeEditRow(card, "CYouGive", true, top - 96f, x0, () => _hsGet, b => _hsGet = b,
                state.Seats[counterSeat].Hand, HsMark);
            BuildTradeEditRow(card, "CYouGet", false, top - 236f, x0, () => _hsGive, b => _hsGive = b, Cap(9), HsMark);
            TradeButton(card, "SendCounter", "Send", TradeGlyphSprites.Counter, new Vector2(1f, 1f),
                new Vector2(-150f, top - 300f), new Vector2(170f, 50f), _hsGive.Total > 0 && _hsGet.Total > 0, true, () =>
                {
                    _hotSeat.Apply(MatchCommand.Respond(counterSeat, TradeResponseKind.Countered, _hsGive, _hsGet));
                    _hsCounterSeat = -1;
                    _hsTradeViewer = trade.From;
                    HsMark();
                }, 18);
        }
    }
}
