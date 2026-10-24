using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private RectTransform _incomingTradePanel;
        private Text _incomingTradeText;
        private Button _incomingAccept;
        private Button _incomingDecline;

        private void BuildIncomingTradePanel(Transform canvasRoot)
        {
            if (ColonistPresentation.IsActive)
            {
                BuildPaintedIncomingTradePanel(canvasRoot);
                return;
            }

            _incomingTradePanel = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "IncomingAiTrade",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-240f, -120f),
                new Vector2(240f, -24f));
            if (ColonistPresentation.IsActive)
                ColonistKenneyHudArt.ApplyWoodBarImage(_incomingTradePanel.GetComponent<Image>());
            else
                _incomingTradePanel.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.28f, 0.94f);

            var layout = _incomingTradePanel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            _incomingTradeText = RunHudUiFactory.CreateText(
                _incomingTradePanel,
                "Offer",
                string.Empty,
                15,
                TextAnchor.UpperCenter,
                ColonistPresentation.IsActive ? ColonistUiKit.Ink : RunHudUiFactory.TextPrimary);
            RunHudUiFactory.AddLayoutElement(_incomingTradeText.gameObject, 44f);

            var row = new GameObject("Buttons");
            row.transform.SetParent(_incomingTradePanel, false);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            RunHudUiFactory.AddLayoutElement(row, 36f);

            _incomingAccept = RunHudUiFactory.CreateButton(row.transform, ColonistHudCopy.Accept, RunHudUiFactory.ButtonFill);
            _incomingAccept.onClick.AddListener(() =>
            {
                if (_controller != null)
                    _controller.RespondToIncomingAiTrade(true);
            });

            _incomingDecline = RunHudUiFactory.CreateButton(row.transform, "Afvis", RunHudUiFactory.ButtonSecondary);
            _incomingDecline.onClick.AddListener(() =>
            {
                if (_controller != null)
                    _controller.RespondToIncomingAiTrade(false);
            });

            _incomingTradePanel.gameObject.SetActive(false);
        }

        private void RefreshIncomingTradePanel(GameState state)
        {
            if (_incomingTradePanel == null)
                return;

            bool show = state.Phase == GamePhase.DayPlayerActions
                && !state.Winner.HasValue
                && state.IncomingAiTrade.HasValue;

            _incomingTradePanel.gameObject.SetActive(show);
            if (!show)
                return;

            var offer = state.IncomingAiTrade.Value;
            if (ColonistPresentation.IsActive)
            {
                RefreshPaintedIncomingTrade(state, offer);
                return;
            }

            _incomingTradeText.text = FormatIncomingOffer(offer);

            bool canAfford = state.PlayerInventory.CanAfford(offer.Receive);
            _incomingAccept.interactable = canAfford;
            _incomingDecline.interactable = true;
        }

        private static string FormatIncomingOffer(AiIncomingTradeOffer offer)
        {
            var sb = new StringBuilder("AI tilbyder bytte\n");
            sb.Append("Du giver: ");
            sb.Append(FormatBundleShort(offer.Receive));
            sb.Append("\nDu får: ");
            sb.Append(FormatBundleShort(offer.Give));
            return sb.ToString();
        }

        private static string FormatBundleShort(ResourceBundle bundle)
        {
            var sb = new StringBuilder();
            foreach (var pair in bundle.EnumerateNonZero())
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(pair.amount);
                sb.Append(' ');
                sb.Append(ResourceDisplayNames.GetEnglish(pair.type));
            }

            return sb.Length == 0 ? "—" : sb.ToString();
        }
    }
}
