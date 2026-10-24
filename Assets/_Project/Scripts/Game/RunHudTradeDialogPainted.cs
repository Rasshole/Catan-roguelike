using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v138 single-player trade card (painted HUD): icons + numbers only. Rate badges on top, "you give" (red arrow)
    /// and "you get" (green arrow) icon rows, bank / offer / clear buttons, then the opponent's answer as a compact
    /// row (✓ / ✗ and a confirm). The AI's incoming offer uses the same kit from YOUR side: what you give is red.
    /// </summary>
    public sealed partial class RunHudController
    {
        private ResourceBundle _spGive;
        private ResourceBundle _spGet;
        private int _spGiveAny;
        private int _spGetAny;
        private bool _spPickOpen;
        private ResourceBundle _spPick;
        private string _spTradeKey;
        private RectTransform _spTradeContent;
        private string _incomingKey;
        private RectTransform _incomingContent;

        private static readonly Color OpponentColor = new(0.92f, 0.32f, 0.28f, 1f);
        private const float SpTradeWidth = 780f;

        /// <summary>Tests/captures: fill the painted trade rows (as clicks on the icons would).</summary>
        public void SetTradeTermsForTests(ResourceBundle give, ResourceBundle get, int giveAny = 0, int getAny = 0)
        {
            _spGive = give;
            _spGet = get;
            _spGiveAny = giveAny;
            _spGetAny = getAny;
            _spTradeKey = null;
            UpdateTradeInfo();
        }

        public RectTransform TradeDialogRootForTests => _tradeDialogRoot;
        public RectTransform IncomingTradePanelForTests => _incomingTradePanel;

        private void BuildPaintedTradeDialogShell()
        {
            var card = CreateCardFrame(_canvas.transform, "TradeDialog", wide: true);
            var img = card.GetComponent<Image>();
            var sliced = ColonistRoguelikeArt.GetCardFrameSliced(true);
            if (sliced != null)
            {
                img.sprite = sliced;
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
            }

            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(5f, -7f);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(-150f, 40f), new Vector2(SpTradeWidth, 540f));
            _tradeDialogRoot = card;
        }

        private static string BundleKey(ResourceBundle b) =>
            string.Join(",", TradeResources.Select(r => b[r]));

        private int SpRate(GameState state, ResourceType r) =>
            BankTradeCalculator.GetGiveRateForResource(state, PlayerId.Human, r);

        /// <summary>v139: the AI opponent has no civilization — a neutral leader in the opponent's red ring.</summary>
        private static Image SpOpponentPortrait(RectTransform parent, string name, Vector2 anchor, Vector2 pos, float size) =>
            BuildTradePortrait(parent, name, OpponentColor, NeutralLeaderSprite(), "AI", Color.white, anchor, pos, size);

        private void RebuildPaintedTradeDialog(GameState state)
        {
            if (_tradeDialogRoot == null || !_tradeDialogRoot.gameObject.activeSelf)
                return;
            var inv = state.PlayerInventory;
            string rates = string.Join(",", TradeResources.Select(r => SpRate(state, r)));
            string pending = state.PendingPlayerTrade.HasValue
                ? $"{BundleKey(state.PendingPlayerTrade.Value.Give)}>{BundleKey(state.PendingPlayerTrade.Value.Receive)}" +
                  $"+{state.PendingPlayerTrade.Value.GiveAny}/{state.PendingPlayerTrade.Value.ReceiveAny}:" +
                  string.Join(",", state.PendingAiTradeResponses.Select(x => x.Accept + BundleKey(x.Choice)))
                : "-";
            if (!state.PendingPlayerTrade.HasValue)
                _spPickOpen = false;
            string key = $"{BundleKey(_spGive)}|{BundleKey(_spGet)}|{_spGiveAny}/{_spGetAny}|{BundleKey(inv)}|{rates}|{pending}" +
                         $"|{_spPickOpen}:{BundleKey(_spPick)}";
            if (key == _spTradeKey && _spTradeContent != null)
                return;
            _spTradeKey = key;
            if (_spTradeContent != null)
            {
                // Detach first: Destroy is deferred and the stale buttons must not be found or clicked this frame.
                _spTradeContent.gameObject.SetActive(false);
                _spTradeContent.SetParent(null, false);
                ColonistSceneDestroy.SafeDestroy(_spTradeContent.gameObject);
            }
            var content = new GameObject("TradeContent").AddComponent<RectTransform>();
            content.SetParent(_tradeDialogRoot, false);
            Stretch(content);
            _spTradeContent = content;

            bool hasPending = state.PendingPlayerTrade.HasValue;
            float pickH = hasPending && _spPickOpen ? TradePickerHeight + 10f : 0f;
            float h = (hasPending ? 670f : 540f) + pickH;
            _tradeDialogRoot.sizeDelta = new Vector2(SpTradeWidth, h);

            void Changed()
            {
                if (_controller != null)
                    RebuildPaintedTradeDialog(_controller.State);
            }

            _spGiveAny = Mathf.Min(_spGiveAny, GiveWildLimitSp(inv));
            var kicker = RunHudUiFactory.CreateText(content, "Kicker", "TRADE", 16, TextAnchor.MiddleCenter,
                TradeKickerInk, FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(500f, 22f));
            TradeButton(content, "TradeClose", "", TradeGlyphSprites.Cross, new Vector2(1f, 1f), TradeClosePos,
                TradeCloseSize, true, false, () => SetTradeDialogVisible(false));
            BuildTradeRateBadges(content, -96f, r => SpRate(state, r));
            float x0 = TradeRowX0(SpTradeWidth);
            // v139: what you get on top (green), what you give below (red), each with the wildcard card.
            BuildTradeEditRow(content, "Get", false, -190f, x0, () => _spGet, b => _spGet = b, Cap(9), Changed,
                new TradeWildSlot { Get = () => _spGetAny, Set = n => _spGetAny = n, Limit = _spGiveAny > 0 ? 0 : TradeWildcards.MaxWildcards });
            BuildTradeEditRow(content, "Give", true, -318f, x0, () => _spGive, b => _spGive = b, inv, Changed,
                new TradeWildSlot
                {
                    Get = () => _spGiveAny, Set = n => _spGiveAny = n,
                    Limit = _spGetAny > 0 ? 0 : GiveWildLimitSp(inv)
                });

            bool bankOk = _spGiveAny == 0 && _spGetAny == 0
                          && TryReadBankTrade(_spGive, _spGet, r => SpRate(state, r), out var giveType, out var getType,
                              out int amount) && inv.CanAfford(_spGive);
            TryReadBankTrade(_spGive, _spGet, r => SpRate(state, r), out giveType, out getType, out amount);
            int shownRate = TradeResources.Any(r => _spGive[r] > 0)
                ? SpRate(state, TradeResources.First(r => _spGive[r] > 0))
                : TradeResources.Min(r => SpRate(state, r));
            float by = h - 482f;
            TradeButton(content, "BankTrade", $"Bank {shownRate}:1", null, new Vector2(0.5f, 0f), new Vector2(-196f, by),
                new Vector2(180f, 54f), bankOk, false, () =>
                {
                    if (_controller != null && _controller.TryBankTrade(giveType, getType, amount))
                    {
                        _spGive = ResourceBundle.Zero;
                        _spGet = ResourceBundle.Zero;
                    }

                    UpdateTradeInfo();
                }, 19);
            var draft = new PlayerTradeOffer(_spGive, _spGet, _spGiveAny, _spGetAny);
            bool offerOk = PlayerTradeResolver.IsValidOffer(state, draft);
            TradeButton(content, "OfferAi", "Offer", TradeGlyphSprites.Counter, new Vector2(0.5f, 0f),
                new Vector2(20f, by), new Vector2(200f, 54f), offerOk, true, ExecutePaintedTradeProposal, 19);
            TradeButton(content, "TradeClear", "", TradeGlyphSprites.Cross, new Vector2(0.5f, 0f), new Vector2(178f, by),
                new Vector2(64f, 54f), _spGive.Total + _spGet.Total + _spGiveAny + _spGetAny > 0, false, () =>
                {
                    _spGive = ResourceBundle.Zero;
                    _spGet = ResourceBundle.Zero;
                    _spGiveAny = _spGetAny = 0;
                    Changed();
                });

            if (!hasPending)
                return;
            var offer = state.PendingPlayerTrade.Value;
            var rule = CreateImage(content, "ResponsesRule", null);
            rule.color = ColonistUiKit.Rule;
            SetRect(rule.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, by - 44f), new Vector2(640f, 2f));
            float y = by - 92f;
            SpOpponentPortrait(content, "OpponentDisc", new Vector2(0f, 0f), new Vector2(112f, y), 58f);
            var name = RunHudUiFactory.CreateText(content, "OpponentName", ColonistHudCopy.Opponent, 19,
                TextAnchor.MiddleLeft, ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(206f, y), new Vector2(110f, 28f));
            var response = state.PendingAiTradeResponses.FirstOrDefault();
            bool answered = state.PendingAiTradeResponses.Count > 0;
            bool accepts = answered && response.Accept;
            // From your side: what you get first, then what you give (the AI's wildcard pick once it accepted).
            float x = 266f;
            x += BuildTradeTerms(content, "OfferGet", false, offer.Receive, new Vector2(0f, 0f), x, y, 34f, offer.ReceiveAny) + 12f;
            var giveShown = offer.Give;
            int giveAnyShown = offer.GiveAny;
            if (accepts && response.Choice.Total > 0)
            {
                giveShown.Add(response.Choice);
                giveAnyShown = 0;
            }

            BuildTradeTerms(content, "OfferGive", true, giveShown, new Vector2(0f, 0f), x, y, 34f, giveAnyShown);
            if (answered)
                TradeStatusMark(content, "OpponentStatus", accepts ? TradeGlyphSprites.Check : TradeGlyphSprites.Cross,
                    new Vector2(1f, 0f), new Vector2(-224f, y), 44f);
            if (accepts && !_spPickOpen)
            {
                var ai = response.Ai;
                TradeButton(content, "AcceptAi", "Trade", TradeGlyphSprites.CheckCream, new Vector2(1f, 0f),
                    new Vector2(-122f, y), new Vector2(140f, 50f), true, true, () =>
                    {
                        if (offer.ReceiveAny > 0)
                        {
                            // v139: "give me N of anything" — pick from what the AI can spare.
                            _spPickOpen = true;
                            _spPick = ResourceBundle.Zero;
                            Changed();
                            return;
                        }

                        CompleteSpTrade(ai, ResourceBundle.Zero);
                    }, 18);
            }

            if (accepts && _spPickOpen)
            {
                var ai = response.Ai;
                var pool = PlayerTradeResolver.AiWildPool(state, offer, response.Choice);
                BuildTradeWildPicker(content, $"Pick {offer.ReceiveAny} from {ColonistHudCopy.Opponent}", -(h - pickH - 30f),
                    SpTradeWidth, offer.ReceiveAny, pool, () => _spPick, b => _spPick = b, Changed,
                    () => CompleteSpTrade(ai, _spPick),
                    () =>
                    {
                        _spPickOpen = false;
                        Changed();
                    });
            }
        }

        private int GiveWildLimitSp(ResourceBundle inv) =>
            Mathf.Min(TradeWildcards.MaxWildcards, TradeWildcards.Pool(inv, _spGive, _spGet).Total);

        private void CompleteSpTrade(PlayerId ai, ResourceBundle pick)
        {
            if (_controller != null && _controller.AcceptPlayerTradeWith(ai, pick))
            {
                _spGive = ResourceBundle.Zero;
                _spGet = ResourceBundle.Zero;
                _spGiveAny = _spGetAny = 0;
                _spPickOpen = false;
                _spPick = ResourceBundle.Zero;
            }

            UpdateTradeInfo();
        }

        /// <summary>Tests: whether the single-player wildcard picker is open.</summary>
        public bool TradeWildPickerOpenForTests => _spPickOpen;

        private void ExecutePaintedTradeProposal()
        {
            if (_controller == null)
                return;
            if (!_controller.ProposePlayerTrade(new PlayerTradeOffer(_spGive, _spGet, _spGiveAny, _spGetAny)))
                HudToastBuffer.Push(_controller.State, "Could not send the offer.");
            UpdateTradeInfo();
        }

        // ---------- incoming AI offer ----------

        private void BuildPaintedIncomingTradePanel(Transform canvasRoot)
        {
            var img = CreateImage(canvasRoot, "IncomingAiTrade", null);
            img.preserveAspect = false;
            img.raycastTarget = true;
            ColonistUiKit.Apply(img, UiKitPiece.FrameMedium);
            SetRect(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(-120f, -150f), new Vector2(600f, 156f));
            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(4f, -6f);
            _incomingTradePanel = img.rectTransform;
            _incomingTradePanel.gameObject.SetActive(false);
        }

        private void RefreshPaintedIncomingTrade(GameState state, AiIncomingTradeOffer offer)
        {
            bool canAfford = state.PlayerInventory.CanAfford(offer.Receive);
            string key = $"{BundleKey(offer.Give)}>{BundleKey(offer.Receive)}:{canAfford}";
            if (key == _incomingKey && _incomingContent != null)
                return;
            _incomingKey = key;
            if (_incomingContent != null)
            {
                // Detach first: Destroy is deferred and the stale buttons must not be found or clicked this frame.
                _incomingContent.gameObject.SetActive(false);
                _incomingContent.SetParent(null, false);
                ColonistSceneDestroy.SafeDestroy(_incomingContent.gameObject);
            }
            var content = new GameObject("IncomingContent").AddComponent<RectTransform>();
            content.SetParent(_incomingTradePanel, false);
            Stretch(content);
            _incomingContent = content;

            SpOpponentPortrait(content, "OpponentDisc", new Vector2(0f, 0.5f), new Vector2(70f, -6f), 66f);
            var kicker = RunHudUiFactory.CreateText(content, "Kicker", "OPPONENT OFFERS YOU", 13, TextAnchor.MiddleLeft,
                ColonistUiKit.InkGold, FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(270f, -34f), new Vector2(300f, 20f));
            // v139: your side, "you get" (what the AI gives, green) above "you give" (what the AI asks, red).
            BuildTradeTerms(content, "YouGet", false, offer.Give, new Vector2(0f, 0.5f), 122f, 10f, 38f);
            BuildTradeTerms(content, "YouGive", true, offer.Receive, new Vector2(0f, 0.5f), 122f, -34f, 38f);
            _incomingAccept = TradeButton(content, "IncomingAccept", "", TradeGlyphSprites.CheckCream, new Vector2(1f, 0.5f),
                new Vector2(-116f, -10f), new Vector2(62f, 54f), canAfford, true, () =>
                {
                    if (_controller != null)
                        _controller.RespondToIncomingAiTrade(true);
                });
            _incomingDecline = TradeButton(content, "IncomingDecline", "", TradeGlyphSprites.Cross, new Vector2(1f, 0.5f),
                new Vector2(-46f, -10f), new Vector2(62f, 54f), true, false, () =>
                {
                    if (_controller != null)
                        _controller.RespondToIncomingAiTrade(false);
                });
        }
    }
}
