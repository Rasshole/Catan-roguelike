using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v143: full-screen pick overlays (civilization pick, quest &amp; trait pick) get a "Show board" toggle that tucks the
    /// cards away so the map can be studied, and a "Back to cards" button to bring them back.
    /// </summary>
    public sealed partial class RunHudController
    {
        public const string PickHideButtonName = "PickHide";
        public const string PickShowButtonName = "PickShow";

        private bool _hsPickHidden;
        private bool _civPickHidden;

        public bool PickOverlayHiddenForTests => _hotSeat != null ? _hsPickHidden : _civPickHidden;

        // ---------- pass & play (party civ / quest picks) ----------

        private void AddHotSeatPickHideButton()
        {
            // Sits just above the pick modal's top-right corner (PartyModal centre is offset -190, +6).
            var b = AddPeekButton(_hotSeatRoot, PickHideButtonName, "Show board", new Vector2(0.5f, 0.5f),
                new Vector2(330f, 452f), new Vector2(220f, 52f), green: false);
            b.onClick.AddListener(() =>
            {
                _hsPickHidden = true;
                _hotSeatDirty = true;
            });
        }

        private void BuildHotSeatPickPeekBar(MatchState state)
        {
            string what = state.Phase == MatchPhase.CivPick ? "civilizations" : "quest & trait";
            var bar = CreateImage(_hotSeatRoot, "PickPeekBar", null);
            bar.color = new Color(0.10f, 0.07f, 0.04f, 0.78f);
            bar.raycastTarget = true;
            SetRect(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(-190f, 70f), new Vector2(760f, 84f));
            var label = RunHudUiFactory.CreateText(bar.rectTransform, "Hint",
                $"{state.Seats[state.ActingSeat].Name}: viewing the board", 20, TextAnchor.MiddleLeft,
                ColonistUiKit.Cream, FontStyle.Italic);
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(360f, 40f));
            var b = AddPeekButton(bar.rectTransform, PickShowButtonName, "Back to " + what, new Vector2(1f, 0.5f),
                new Vector2(-170f, 0f), new Vector2(300f, 60f), green: true);
            b.onClick.AddListener(() =>
            {
                _hsPickHidden = false;
                _hotSeatDirty = true;
            });
        }

        // ---------- single player (civilization pick) ----------

        private void AddCivPickPeekButtons(RectTransform root)
        {
            var hide = AddPeekButton(root, PickHideButtonName, "Show board", new Vector2(1f, 1f),
                new Vector2(-150f, -60f), new Vector2(220f, 52f), green: false);
            hide.onClick.AddListener(() => SetCivPickHidden(true));
            var show = AddPeekButton(root, PickShowButtonName, "Back to civilizations", new Vector2(0.5f, 0f),
                new Vector2(0f, 80f), new Vector2(320f, 64f), green: true);
            show.onClick.AddListener(() => SetCivPickHidden(false));
            ApplyCivPickHidden(root);
        }

        private void SetCivPickHidden(bool hidden)
        {
            _civPickHidden = hidden;
            if (_civPickRoot != null)
                ApplyCivPickHidden(_civPickRoot);
        }

        private void ApplyCivPickHidden(RectTransform root)
        {
            foreach (Transform child in root)
                child.gameObject.SetActive(child.name == PickShowButtonName ? _civPickHidden : !_civPickHidden);
            var dim = root.GetComponent<Image>();
            if (dim != null)
            {
                dim.enabled = !_civPickHidden;
                dim.raycastTarget = !_civPickHidden;
            }
        }

        private static Button AddPeekButton(RectTransform parent, string name, string label, Vector2 anchor,
            Vector2 pos, Vector2 size, bool green)
        {
            var button = RunHudUiFactory.CreateButton(parent, label, RunHudUiFactory.ButtonFill);
            button.name = name;
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            ColonistKenneyHudArt.ApplyColonistWarmButton(button.GetComponent<Image>(), primaryGreen: green);
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = green ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                text.fontSize = 20;
            }

            return button;
        }
    }
}
