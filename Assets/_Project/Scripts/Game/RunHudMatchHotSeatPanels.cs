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

        // ---------- trade board ----------

        private string Terms(ResourceBundle b) =>
            b.Total == 0
                ? "nothing"
                : string.Join(" + ", MatchController.AllResources.Where(r => b[r] > 0).Select(r => $"{b[r]} {ResourceLabel(r)}"));

        private void BuildTradeBoard()
        {
            var s = _hotSeat;
            var state = s.State;
            var trade = state.ActiveTrade;
            if (trade == null && (!_hsTradeOpen || state.Phase != MatchPhase.Main || s.Acting.IsAi || s.PassScreenPending))
            {
                if (trade == null)
                    _hsCounterSeat = -1;
                return;
            }

            if (trade != null && (s.PassScreenPending || s.IsOver))
                return;

            int seat = state.ActingSeat;
            float rows = trade == null ? 0 : state.Seats.Count - 1;
            var card = HsModal("TradeBoard", new Vector2(980f, trade == null ? 520f : 300f + rows * 74f
                + (_hsCounterSeat >= 0 ? 200f : 0f)), HsModalOffset + new Vector2(0f, trade == null ? 0f : 40f));
            HsText(card, "Kicker", "TRADE BOARD", 16, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(700f, 22f),
                ColonistUiKit.InkGold, FontStyle.Bold);
            HsButton(card, "TradeClose", "✕", new Vector2(1f, 1f), new Vector2(-62f, -54f), new Vector2(56f, 50f),
                trade == null, false, () => _hsTradeOpen = false, 22);

            if (trade == null)
            {
                BuildTradeComposer(card, seat);
                return;
            }

            var from = state.Seats[trade.From];
            HsText(card, "Offer", $"{from.Name} gives {Terms(trade.Give)}  ·  wants {Terms(trade.Get)}", 23,
                new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(900f, 34f), ColonistUiKit.Ink, FontStyle.Bold);
            HsText(card, "Hint", "Everyone at the table answers here; the offerer confirms one partner.", 16,
                new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(900f, 24f), ColonistUiKit.InkMuted,
                FontStyle.Italic);
            int row = 0;
            for (int i = 0; i < state.Seats.Count; i++)
            {
                if (i == trade.From)
                    continue;
                int responder = i;
                var other = state.Seats[i];
                var response = trade.Responses[i];
                float y = -170f - row++ * 74f;
                var disc = CreateDisc(card, "TradeDisc_" + i, PlayerPieceVisuals.ColorForSeat(i), 46f);
                SetRect(disc.rectTransform, new Vector2(0f, 1f), new Vector2(70f, y), new Vector2(46f, 46f));
                HsText(card, "TradeName_" + i, other.Name + (other.IsAi ? " (AI)" : ""), 20, new Vector2(0f, 1f),
                    new Vector2(200f, y), new Vector2(200f, 30f), ColonistUiKit.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
                string status = response.Kind switch
                {
                    TradeResponseKind.Accepted => "accepts",
                    TradeResponseKind.Declined => "declines",
                    TradeResponseKind.Countered =>
                        $"counters: you give {Terms(response.CounterGive)}, get {Terms(response.CounterGet)}",
                    _ => "thinking…"
                };
                var statusColor = response.Kind switch
                {
                    TradeResponseKind.Accepted => new Color(0.16f, 0.45f, 0.18f, 1f),
                    TradeResponseKind.Declined => TimerPulseRed,
                    _ => ColonistUiKit.InkMuted
                };
                HsText(card, "TradeStatus_" + i, status, 17, new Vector2(0f, 1f), new Vector2(470f, y),
                    new Vector2(360f, 44f), statusColor, FontStyle.Italic, TextAnchor.MiddleLeft);

                bool canAnswer = !other.IsAi && response.Kind == TradeResponseKind.Pending;
                if (canAnswer)
                {
                    HsButton(card, "Accept_" + i, "Accept", new Vector2(1f, 1f), new Vector2(-330f, y),
                        new Vector2(110f, 46f), other.Hand.CanAfford(trade.Get), true,
                        () => _hotSeat.Apply(MatchCommand.Respond(responder, TradeResponseKind.Accepted)), 16);
                    HsButton(card, "Decline_" + i, "Decline", new Vector2(1f, 1f), new Vector2(-210f, y),
                        new Vector2(110f, 46f), true, false,
                        () => _hotSeat.Apply(MatchCommand.Respond(responder, TradeResponseKind.Declined)), 16);
                    HsButton(card, "Counter_" + i, "Counter", new Vector2(1f, 1f), new Vector2(-90f, y),
                        new Vector2(110f, 46f), true, false, () =>
                        {
                            _hsCounterSeat = responder;
                            _hsGive = trade.Give;
                            _hsGet = trade.Get;
                        }, 16);
                }
                else if (response.Kind == TradeResponseKind.Accepted || response.Kind == TradeResponseKind.Countered)
                {
                    HsButton(card, "Confirm_" + i, "Trade", new Vector2(1f, 1f), new Vector2(-120f, y),
                        new Vector2(170f, 46f), !state.Seats[trade.From].IsAi, true,
                        () => _hotSeat.Apply(MatchCommand.Confirm(trade.From, responder)), 17);
                }
            }

            if (_hsCounterSeat >= 0 && _hsCounterSeat != trade.From)
            {
                float y0 = -170f - row * 74f - 10f;
                HsText(card, "CounterTitle", $"{state.Seats[_hsCounterSeat].Name}'s counter (as {from.Name} would trade)",
                    17, new Vector2(0.5f, 1f), new Vector2(0f, y0), new Vector2(800f, 24f), ColonistUiKit.InkGold,
                    FontStyle.Bold);
                BuildStepperRow(card, "CGive", $"{from.Name} gives", y0 - 50f, () => _hsGive, b => _hsGive = b, 9);
                BuildStepperRow(card, "CGet", $"{from.Name} gets", y0 - 110f, () => _hsGet, b => _hsGet = b,
                    state.Seats[_hsCounterSeat].Hand);
                int counterSeat = _hsCounterSeat;
                HsButton(card, "SendCounter", "Send counter", new Vector2(1f, 1f), new Vector2(-120f, y0 - 160f),
                    new Vector2(200f, 46f), _hsGive.Total > 0 && _hsGet.Total > 0, true, () =>
                    {
                        _hotSeat.Apply(MatchCommand.Respond(counterSeat, TradeResponseKind.Countered, _hsGive, _hsGet));
                        _hsCounterSeat = -1;
                    }, 17);
            }

            HsButton(card, "CancelOffer", "Withdraw offer", new Vector2(0.5f, 0f), new Vector2(0f, 52f),
                new Vector2(240f, 50f), !from.IsAi, false, () =>
                {
                    _hsCounterSeat = -1;
                    _hotSeat.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, trade.From));
                });
        }

        private void BuildTradeComposer(RectTransform card, int seat)
        {
            var s = _hotSeat;
            var me = s.State.Seats[seat];
            HsText(card, "Title", "Offer the table a trade, or trade with the bank", 24, new Vector2(0.5f, 1f),
                new Vector2(0f, -82f), new Vector2(800f, 34f), ColonistUiKit.Ink, FontStyle.Bold);
            var rates = MatchController.AllResources.Select(r => (r, s.Match.BankRatio(seat, r))).ToList();
            string harbours = rates.All(x => x.Item2 == 4)
                ? "Bank 4:1 · build on a harbour for 3:1 or 2:1"
                : "Bank: " + string.Join("  ", rates.Select(x => $"{ResourceLabel(x.r)} {x.Item2}:1"));
            HsText(card, "Rates", harbours, 16, new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(880f, 22f),
                ColonistUiKit.InkMuted, FontStyle.Italic);
            BuildStepperRow(card, "Give", "You give", -180f, () => _hsGive, b => _hsGive = b, me.Hand);
            BuildStepperRow(card, "Get", "You get", -290f, () => _hsGet, b => _hsGet = b, 9);

            // Bank: exactly ratio × one resource for one of another.
            var giveKinds = MatchController.AllResources.Where(r => _hsGive[r] > 0).ToList();
            var getKinds = MatchController.AllResources.Where(r => _hsGet[r] > 0).ToList();
            bool bankOk = giveKinds.Count == 1 && getKinds.Count == 1 && giveKinds[0] != getKinds[0] && _hsGet.Total == 1
                          && _hsGive.Total == s.Match.BankRatio(seat, giveKinds[0]);
            string bankLabel = giveKinds.Count == 1 ? $"Bank {s.Match.BankRatio(seat, giveKinds[0])}:1" : "Bank trade";
            HsButton(card, "BankTrade", bankLabel, new Vector2(0.5f, 0f), new Vector2(-150f, 56f), new Vector2(220f, 54f),
                bankOk, false, () =>
                {
                    var r = s.Apply(MatchCommand.Trade(seat, giveKinds[0], getKinds[0]));
                    if (r.Ok)
                    {
                        _hsGive = ResourceBundle.Zero;
                        _hsGet = ResourceBundle.Zero;
                    }
                });
            bool offerOk = _hsGive.Total > 0 && _hsGet.Total > 0 && me.Hand.CanAfford(_hsGive)
                           && !MatchController.AllResources.Any(r => _hsGive[r] > 0 && _hsGet[r] > 0);
            HsButton(card, "ProposeTrade", "Offer to players", new Vector2(0.5f, 0f), new Vector2(110f, 56f),
                new Vector2(260f, 54f), offerOk, true, () => s.Apply(MatchCommand.Propose(seat, _hsGive, _hsGet)));
        }

        private void BuildStepperRow(RectTransform card, string key, string label, float y,
            System.Func<ResourceBundle> get, System.Action<ResourceBundle> set, ResourceBundle limit)
        {
            HsText(card, key + "Label", label, 17, new Vector2(0f, 1f), new Vector2(168f, y - 18f), new Vector2(136f, 30f),
                ColonistUiKit.Ink, FontStyle.Bold, TextAnchor.MiddleRight);
            var resources = MatchController.AllResources;
            for (int i = 0; i < resources.Length; i++)
            {
                var r = resources[i];
                float x = 300f + i * 132f;
                var icon = CreateImage(card, key + "Icon_" + r, ColonistHudResourceSprites.Get(r));
                SetRect(icon.rectTransform, new Vector2(0f, 1f), new Vector2(x, y + 2f), new Vector2(44f, 44f));
                int n = get()[r];
                HsText(card, key + "N_" + r, n.ToString(), 22, new Vector2(0f, 1f), new Vector2(x, y - 36f),
                    new Vector2(40f, 28f), n > 0 ? ColonistUiKit.Ink : ColonistUiKit.InkMuted, FontStyle.Bold);
                HsButton(card, key + "Less_" + r, "−", new Vector2(0f, 1f), new Vector2(x - 44f, y - 36f),
                    new Vector2(36f, 34f), n > 0, false, () =>
                    {
                        var b = get();
                        b.Add(r, -1);
                        set(b);
                    }, 20);
                HsButton(card, key + "More_" + r, "+", new Vector2(0f, 1f), new Vector2(x + 44f, y - 36f),
                    new Vector2(36f, 34f), n < limit[r], false, () =>
                    {
                        var b = get();
                        b.Add(r, 1);
                        set(b);
                    }, 20);
            }
        }

        private void BuildStepperRow(RectTransform card, string key, string label, float y,
            System.Func<ResourceBundle> get, System.Action<ResourceBundle> set, int cap) =>
            BuildStepperRow(card, key, label, y, get, set,
                new ResourceBundle { Wood = cap, Brick = cap, Wheat = cap, Sheep = cap, Stone = cap });
    }
}
