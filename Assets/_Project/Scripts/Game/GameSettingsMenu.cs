using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v143: a small "Menu" button (bottom-right) that opens a parchment popup with the sound volume slider, a mute toggle and —
    /// while a match runs — "Leave match", which returns cleanly to the start menu (single player and pass &amp; play).
    /// Lives on its own overlay canvas so HUD rebuilds never take it down.
    /// </summary>
    public sealed class GameSettingsMenu : MonoBehaviour
    {
        public const string GearName = "SettingsGear";
        public const string PanelName = "SettingsPanel";
        public const string LeaveName = "SettingsLeaveMatch";

        private Canvas _canvas;
        private RectTransform _panel;
        private Button _leave;
        private Slider _volume;
        private Toggle _mute;
        private RunHudController _hud;

        public RectTransform PanelForTests
        {
            get
            {
                EnsureBuilt();
                return _panel;
            }
        }

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;
        private GameObject _host;

        public void Bind(RunHudController hud)
        {
            _hud = hud;
            Build();
        }

        /// <summary>Rebuild if anything tore the overlay down (it lives on its own scene-root object).</summary>
        private void EnsureBuilt()
        {
            if (_canvas == null || _panel == null)
                Build();
        }

        private void OnDestroy()
        {
            if (_host != null)
                ColonistSceneDestroy.SafeDestroy(_host);
        }

        private void Build()
        {
            if (_canvas != null && _panel != null)
                return;
            if (_host != null)
                ColonistSceneDestroy.SafeDestroy(_host);
            // Own scene-root host: HUD/board rebuilds under the GameManager never take the menu down.
            _host = new GameObject("GameSettingsMenuRoot");
            _canvas = RunHudUiFactory.CreateCanvas(_host.transform);
            _canvas.name = "SettingsCanvas";
            _canvas.sortingOrder = 60;

            var gear = RunHudUiFactory.CreateButton(_canvas.transform, "Menu", RunHudUiFactory.ButtonFill);
            gear.name = GearName;
            // Bottom-right corner: clear of the pass & play seat strip (top-right) and the build bar.
            Place((RectTransform)gear.transform, new Vector2(1f, 0f), new Vector2(-58f, 30f), new Vector2(92f, 42f));
            ColonistKenneyHudArt.ApplyColonistWarmButton(gear.GetComponent<Image>(), primaryGreen: false);
            var gt = gear.GetComponentInChildren<Text>();
            if (gt != null)
            {
                gt.fontSize = 18;
                gt.color = ColonistUiKit.Ink;
            }

            gear.onClick.AddListener(Toggle);

            var panelImg = new GameObject(PanelName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _panel = (RectTransform)panelImg.transform;
            _panel.SetParent(_canvas.transform, false);
            Place(_panel, new Vector2(1f, 0f), new Vector2(-190f, 200f), new Vector2(320f, 270f));
            panelImg.color = new Color(0.94f, 0.88f, 0.74f, 0.98f);
            ColonistUiKit.Apply(panelImg, UiKitPiece.FrameMedium);

            var title = RunHudUiFactory.CreateText(_panel, "Title", "Settings", 24, TextAnchor.MiddleCenter,
                ColonistUiKit.Ink, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(280f, 34f));

            var volLabel = RunHudUiFactory.CreateText(_panel, "VolumeLabel", "Sound volume", 18, TextAnchor.MiddleLeft,
                ColonistUiKit.Ink);
            Place(volLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(260f, 26f));

            var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGo.name = "VolumeSlider";
            sliderGo.transform.SetParent(_panel, false);
            Place((RectTransform)sliderGo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(260f, 22f));
            _volume = sliderGo.GetComponent<Slider>();
            _volume.minValue = 0f;
            _volume.maxValue = 1f;
            _volume.value = GameAudio.Volume;
            TintSlider(sliderGo);
            _volume.onValueChanged.AddListener(v =>
            {
                GameAudio.Volume = v;
                if (_mute != null && v > 0.001f && GameAudio.Muted)
                    _mute.isOn = false;
            });

            var toggleGo = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleGo.name = "MuteToggle";
            toggleGo.transform.SetParent(_panel, false);
            Place((RectTransform)toggleGo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(260f, 26f));
            _mute = toggleGo.GetComponent<Toggle>();
            _mute.isOn = GameAudio.Muted;
            var tl = toggleGo.GetComponentInChildren<Text>();
            if (tl != null)
            {
                tl.text = "Mute all sounds";
                tl.font = title.font;
                tl.fontSize = 18;
                tl.color = ColonistUiKit.Ink;
            }

            _mute.onValueChanged.AddListener(m =>
            {
                GameAudio.Muted = m;
                if (!m)
                    GameAudio.Play(Sfx.Click);
            });

            _leave = RunHudUiFactory.CreateButton(_panel, "Leave match", RunHudUiFactory.ButtonFill);
            _leave.name = LeaveName;
            Place((RectTransform)_leave.transform, new Vector2(0.5f, 0f), new Vector2(-70f, 42f), new Vector2(150f, 48f));
            ColonistKenneyHudArt.ApplyColonistWarmButton(_leave.GetComponent<Image>(), primaryGreen: false);
            StyleLabel(_leave, ColonistUiKit.Ink);
            _leave.onClick.AddListener(() =>
            {
                Close();
                if (_hud != null)
                    _hud.LeaveMatchToMenu();
            });

            var close = RunHudUiFactory.CreateButton(_panel, "Close", RunHudUiFactory.ButtonFill);
            close.name = "SettingsClose";
            Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(84f, 42f), new Vector2(120f, 48f));
            ColonistKenneyHudArt.ApplyColonistWarmButton(close.GetComponent<Image>(), primaryGreen: true);
            StyleLabel(close, ColonistUiKit.Cream);
            close.onClick.AddListener(Close);

            SetLayer(_canvas.gameObject, RunHudController.HudCaptureUiLayer);
            _panel.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            EnsureBuilt();
            if (_panel == null)
                return;
            bool open = !_panel.gameObject.activeSelf;
            _panel.gameObject.SetActive(open);
            if (open)
            {
                _panel.SetAsLastSibling();
                if (_leave != null)
                    _leave.gameObject.SetActive(_hud != null && _hud.InMatchForMenu);
            }
        }

        public void Close()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        private static void StyleLabel(Button b, Color c)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t == null)
                return;
            t.color = c;
            t.fontSize = 18;
        }

        private static void TintSlider(GameObject slider)
        {
            foreach (var img in slider.GetComponentsInChildren<Image>(true))
            {
                if (img.name == "Background")
                    img.color = new Color(0.55f, 0.42f, 0.26f, 1f);
                else if (img.name == "Fill")
                    img.color = new Color(0.30f, 0.55f, 0.28f, 1f);
                else if (img.name == "Handle")
                    img.color = new Color(0.98f, 0.92f, 0.76f, 1f);
            }
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform)
                SetLayer(t.gameObject, layer);
        }
    }
}
