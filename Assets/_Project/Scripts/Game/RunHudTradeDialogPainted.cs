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
        private string _spTradeKey;
        private RectTransform _spTradeContent;
        private string _incomingKey;
        private RectTransform _incomingContent;

        private static readonly Color OpponentColor = new(0.92f, 0.32f, 0.28f, 1f);

        /// <summary>Tests/captures: fill the painted trade rows (as clicks on the icons would).</summary>
        public void SetTradeTermsForTests(ResourceBundle give, ResourceBundle get)
        {
            _spGive = give;
            _spGet = get;
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
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(-150f, 40f), new Vector2(780f, 560f));
            _tradeDialogRoot = card;
        }

        private static string BundleKey(ResourceBundle b) =>
            string.Join(",", TradeResources.Select(r => b[r]));

        private int SpRate(GameState state, ResourceType r) =>
            BankTradeCalculator.GetGiveRateForResource(state, PlayerId.Human, r);

        private void RebuildPaintedTradeDialog(GameState state)
        {
            if (_tradeDialogRoot == null || !_tradeDialogRoot.gameObject.activeSelf)
                return;
            var inv = state.PlayerInventory;
            string rates = string.Join(",", TradeResources.Select(r => SpRate(state, r)));
            string pending = state.PendingPlayerTrade.HasValue
                ? $"{BundleKey(state.PendingPlayerTrade.Value.Give)}>{BundleKey(state.PendingPlayerTrade.Value.Receive)}:" +
                  string.Join(",", state.PendingAiTradeResponses.Select(x => x.Accept))
                : "-";
            string key = $"{BundleKey(_spGive)}|{BundleKey(_spGet)}|{BundleKey(inv)}|{rates}|{pending}";
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
            float h = hasPending ? 690f : 560f;
            _tradeDialogRoot.sizeDelta = new Vector2(780f, h);

            void Changed()
            {
                if (_controller != null)
                    RebuildPaintedTradeDialog(_controller.State);
            }

            var kicker = RunHudUiFactory.CreateText(content, "Kicker", "TRADE", 16, TextAnchor.MiddleCenter,
                TradeKickerInk, FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(500f, 22f));
            TradeButton(content, "TradeClose", "", TradeGlyphSprites.Cross, new Vector2(1f, 1f), TradeClosePos,
                TradeCloseSize, true, false, () => SetTradeDialogVisible(false));
            BuildTradeRateBadges(content, -96f, r => SpRate(state, r));
            const float x0 = 142f;
            BuildTradeEditRow(content, "Give", true, -196f, x0, () => _spGive, b => _spGive = b, inv, Changed);
            BuildTradeEditRow(content, "Get", false, -340f, x0, () => _spGet, b => _spGet = b, Cap(9), Changed);

            bool bankOk = TryReadBankTrade(_spGive, _spGet, r => SpRate(state, r), out var giveType, out var getType,
                out int amount) && inv.CanAfford(_spGive);
            int shownRate = TradeResources.Any(r => _spGive[r] > 0)
                ? SpRate(state, TradeResources.First(r => _spGive[r] > 0))
                : TradeResources.Min(r => SpRate(state, r));
            float by = hasPending ? 200f : 62f;
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
            bool offerOk = _spGive.Total > 0 && _spGet.Total > 0 && inv.CanAfford(_spGive)
                           && !TradeResources.Any(r => _spGive[r] > 0 && _spGet[r] > 0);
            TradeButton(content, "OfferAi", "Offer", TradeGlyphSprites.Counter, new Vector2(0.5f, 0f),
                new Vector2(20f, by), new Vector2(200f, 54f), offerOk, true, ExecutePaintedTradeProposal, 19);
            TradeButton(content, "TradeClear", "", TradeGlyphSprites.Cross, new Vector2(0.5f, 0f), new Vector2(178f, by),
                new Vector2(64f, 54f), _spGive.Total + _spGet.Total > 0, false, () =>
                {
                    _spGive = ResourceBundle.Zero;
                    _spGet = ResourceBundle.Zero;
                    Changed();
                });

            if (!hasPending)
                return;
            var offer = state.PendingPlayerTrade.Value;
            var rule = CreateImage(content, "ResponsesRule", null);
            rule.color = ColonistUiKit.Rule;
            SetRect(rule.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 158f), new Vector2(640f, 2f));
            float y = 112f;
            var disc = CreateDisc(content, "OpponentDisc", OpponentColor, 46f);
            SetRect(disc.rectTransform, new Vector2(0f, 0f), new Vector2(110f, y), new Vector2(46f, 46f));
            AiTag(disc.rectTransform, 15);
            var name = RunHudUiFactory.CreateText(content, "OpponentName", ColonistHudCopy.Opponent, 19,
                TextAnchor.MiddleLeft, ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(200f, y), new Vector2(110f, 28f));
            float x = 262f;
            x += BuildTradeTerms(content, "OfferGive", true, offer.Give, new Vector2(0f, 0f), x, y, 34f) + 12f;
            BuildTradeTerms(content, "OfferGet", false, offer.Receive, new Vector2(0f, 0f), x, y, 34f);
            var response = state.PendingAiTradeResponses.FirstOrDefault();
            bool answered = state.PendingAiTradeResponses.Count > 0;
            bool accepts = answered && response.Accept;
            if (answered)
                TradeStatusMark(content, "OpponentStatus", accepts ? TradeGlyphSprites.Check : TradeGlyphSprites.Cross,
                    new Vector2(1f, 0f), new Vector2(-230f, y), 44f);
            if (accepts)
            {
                var ai = response.Ai;
                TradeButton(content, "AcceptAi", "Trade", TradeGlyphSprites.CheckCream, new Vector2(1f, 0f),
                    new Vector2(-124f, y), new Vector2(150f, 50f), true, true, () =>
                    {
                        if (_controller != null && _controller.AcceptPlayerTradeWith(ai))
                        {
                            _spGive = ResourceBundle.Zero;
                            _spGet = ResourceBundle.Zero;
                        }

                        UpdateTradeInfo();
                    }, 18);
            }
        }

        private static void AiTag(RectTransform disc, int size)
        {
            var t = RunHudUiFactory.CreateText(disc, "Tag", "AI", size, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Stretch(t.rectTransform);
        }

        private void ExecutePaintedTradeProposal()
        {
            if (_controller == null)
                return;
            if (!_controller.ProposePlayerTrade(new PlayerTradeOffer(_spGive, _spGet)))
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
            SetRect(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(-120f, -150f), new Vector2(620f, 150f));
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

            var disc = CreateDisc(content, "OpponentDisc", OpponentColor, 50f);
            SetRect(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(50f, 50f));
            AiTag(disc.rectTransform, 16);
            var kicker = RunHudUiFactory.CreateText(content, "Kicker", "OPPONENT OFFERS YOU", 13, TextAnchor.MiddleLeft,
                ColonistUiKit.InkGold, FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(230f, -36f), new Vector2(300f, 20f));
            // Your side: you give what the AI receives (red), you get what the AI gives (green).
            float x = 112f;
            x += BuildTradeTerms(content, "YouGive", true, offer.Receive, new Vector2(0f, 0.5f), x, -14f, 44f) + 18f;
            BuildTradeTerms(content, "YouGet", false, offer.Give, new Vector2(0f, 0.5f), x, -14f, 44f);
            _incomingAccept = TradeButton(content, "IncomingAccept", "", TradeGlyphSprites.CheckCream, new Vector2(1f, 0.5f),
                new Vector2(-122f, -6f), new Vector2(64f, 56f), canAfford, true, () =>
                {
                    if (_controller != null)
                        _controller.RespondToIncomingAiTrade(true);
                });
            _incomingDecline = TradeButton(content, "IncomingDecline", "", TradeGlyphSprites.Cross, new Vector2(1f, 0.5f),
                new Vector2(-50f, -6f), new Vector2(64f, 56f), true, false, () =>
                {
                    if (_controller != null)
                        _controller.RespondToIncomingAiTrade(false);
                });
        }
    }
}
