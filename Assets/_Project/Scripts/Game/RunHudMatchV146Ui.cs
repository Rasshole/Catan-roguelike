using System.Collections.Generic;
using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v146 pass &amp; play polish: public traits on the seat strip (chip + hover tooltip), your own quest always in
    /// view while you wait, the trade countdown, and the HUD motion (panels fade/slide in, the active seat pulses,
    /// VP ticks punch). The table HUD is rebuilt on every change, so motion is keyed to <see cref="HudMotion"/>
    /// start times and re-applied each frame to the fresh objects.
    /// </summary>
    public sealed partial class RunHudController
    {
        public const string TraitChipName = "TraitChip";
        public const string TraitTooltipName = "TraitTooltip";
        public const string OwnQuestCardName = "OwnQuest";
        public const string TradeCountdownName = "TradeCountdown";
        private const float PanelInSeconds = 0.24f;
        private const float VpPunchSeconds = 0.7f;

        private readonly Dictionary<string, float> _hsPanelBorn = new();
        private bool _hsPanelsPrimed;
        private Image[] _hsSeatRows;
        private Text[] _hsVpTexts;
        private int[] _hsVpLast;
        private float[] _hsVpChangedAt;
        private Text _hsTradeCountdownText;
        private RectTransform _hsTradeCountdownFill;
        private float _hsTradeCountdownWidth;
        private int _hsTooltipSeat = -1;

        public RectTransform OwnQuestCardForTests => _hotSeatRoot == null ? null : _hotSeatRoot.Find(OwnQuestCardName) as RectTransform;
        public RectTransform TraitTooltipForTests => _hotSeatRoot == null ? null : _hotSeatRoot.Find(TraitTooltipName) as RectTransform;
        public string TradeCountdownTextForTests => _hsTradeCountdownText != null ? _hsTradeCountdownText.text : null;
        public float SeatRowPulseAlphaForTests(int seat) =>
            _hsSeatRows != null && seat < _hsSeatRows.Length && _hsSeatRows[seat] != null ? _hsSeatRows[seat].color.a : -1f;

        public void ShowTraitTooltipForTests(int seat) => ShowTraitTooltip(seat);
        public void HideTraitTooltipForTests() => HideTraitTooltip();

        private void ResetV146Ui()
        {
            _hsPanelBorn.Clear();
            _hsPanelsPrimed = false;
            _hsVpLast = null;
            _hsVpChangedAt = null;
            _hsTooltipSeat = -1;
        }

        // ---------- traits on the strip ----------

        /// <summary>Traits are public once picked (after the pick phases): a chip under the seat's name.</summary>
        private bool SeatTraitVisible(MatchState state, MatchSeatState seat) =>
            state.Config.PartyRules && !state.IsPicking && seat.Trait != MatchTraitId.None
            && MatchPartyLibrary.Trait(seat.Trait) != null;

        private void BuildSeatTraitChip(RectTransform row, MatchState state, int i, float x0)
        {
            var trait = MatchPartyLibrary.Trait(state.Seats[i].Trait);
            float w = 252f - x0;
            var chip = CreateImage(row, TraitChipName, null);
            chip.preserveAspect = false;
            chip.color = new Color(0.96f, 0.8f, 0.4f, 1f);
            chip.raycastTarget = true;
            SetRect(chip.rectTransform, new Vector2(0f, 0.5f), new Vector2(x0 + w / 2f, -31f), new Vector2(w, 22f));
            var edge = chip.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.45f, 0.28f, 0.08f, 0.9f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            var t = RunHudUiFactory.CreateText(chip.rectTransform, "Text", trait.Name, 13, TextAnchor.MiddleCenter,
                new Color(0.25f, 0.15f, 0.05f, 1f), FontStyle.Bold);
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 9;
            t.resizeTextMaxSize = 13;
            SetRect(t.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 8f, 20f));
            var hover = chip.gameObject.AddComponent<HoverRelay>();
            int seat = i;
            hover.Enter = () => ShowTraitTooltip(seat);
            hover.Exit = HideTraitTooltip;
            var b = chip.gameObject.AddComponent<Button>();
            b.targetGraphic = chip;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                HideTraitTooltip();
                ToggleHotSeatInfo(seat); // tap (touch) opens the seat card, which shows the trait in full
            });
        }

        private void ShowTraitTooltip(int seat)
        {
            HideTraitTooltip();
            if (_hotSeat == null || _hotSeatRoot == null)
                return;
            var state = _hotSeat.State;
            if (seat < 0 || seat >= state.Seats.Count || !SeatTraitVisible(state, state.Seats[seat]))
                return;
            var row = _hotSeatRoot.Find("SeatStrip/Seat_" + seat) as RectTransform;
            if (row == null)
                return;
            var trait = MatchPartyLibrary.Trait(state.Seats[seat].Trait);
            var tip = CreateImage(_hotSeatRoot, TraitTooltipName, null);
            tip.preserveAspect = false;
            ColonistUiKit.Apply(tip, UiKitPiece.FrameMedium);
            tip.raycastTarget = false;
            var rt = tip.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(380f, 132f);
            rt.position = row.TransformPoint(new Vector3(-row.rect.width * 0.5f - 30f, 0f, 0f));
            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            var head = RunHudUiFactory.CreateText(rt, "Head", $"{state.Seats[seat].Name.ToUpperInvariant()}  ·  TRAIT  ·  {trait.Name.ToUpperInvariant()}",
                14, TextAnchor.MiddleLeft, ink, FontStyle.Bold);
            head.raycastTarget = false;
            SetRect(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(330f, 20f));
            var body = RunHudUiFactory.CreateText(rt, "Body", trait.Description, 14, TextAnchor.UpperLeft, ink,
                FontStyle.Normal);
            body.raycastTarget = false;
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 10;
            body.resizeTextMaxSize = 14;
            SetRect(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(330f, 70f));
            _hsTooltipSeat = seat;
            if (!_hsPanelBorn.ContainsKey(TraitTooltipName))
                _hsPanelBorn[TraitTooltipName] = HudMotion.Now;
            SetLayerRecursively(tip.gameObject, HudCaptureUiLayer);
        }

        private void HideTraitTooltip()
        {
            _hsTooltipSeat = -1;
            var tip = _hotSeatRoot == null ? null : _hotSeatRoot.Find(TraitTooltipName);
            if (tip != null)
                ColonistSceneDestroy.SafeDestroy(tip.gameObject);
        }

        // ---------- your quest while you wait ----------

        /// <summary>
        /// Whose quest the device holder may see: the seat holding the device, or (during AI turns and replies)
        /// the human who last held it. Never on a pass screen, so a quest is never shown to the next player.
        /// </summary>
        private int OwnQuestSeat()
        {
            var s = _hotSeat;
            if (s == null || s.IsOver || s.PassScreenPending || !s.State.Config.PartyRules)
                return -1;
            int acting = s.State.ActingSeat;
            if (s.IsHandVisible(acting))
                return acting;
            int last = s.LastRevealedHumanSeat;
            if (last >= 0 && last < s.State.Seats.Count && !s.State.Seats[last].IsAi && (s.Acting.IsAi || s.HumanCount == 1))
                return last;
            if (s.HumanCount == 1)
                for (int i = 0; i < s.State.Seats.Count; i++)
                    if (!s.State.Seats[i].IsAi)
                        return i;
            return -1;
        }

        private void BuildOwnQuestCard()
        {
            int seat = OwnQuestSeat();
            if (seat < 0)
                return;
            var state = _hotSeat.State;
            var me = state.Seats[seat];
            var quest = MatchPartyLibrary.Quest(me.Quest);
            if (quest == null)
                return;
            var panel = CreateImage(_hotSeatRoot, OwnQuestCardName, null);
            panel.preserveAspect = false;
            ColonistUiKit.Apply(panel, UiKitPiece.FrameMedium);
            SetRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(330f, 398f), new Vector2(580f, 92f));
            var ink = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
            int target = Mathf.Max(1, quest.Target);
            int progress = me.QuestDone ? target : Mathf.Clamp(quest.CurrentProgress(_hotSeat.Match, seat), 0, target);
            bool waiting = seat != state.ActingSeat || !_hotSeat.IsHandVisible(seat);
            string who = waiting ? $"YOUR QUEST  ·  {me.Name}" : "YOUR SECRET QUEST";
            HsText(panel.rectTransform, "Title", $"{who}  ·  {quest.Name.ToUpperInvariant()}  ·  {quest.Points} VP", 14,
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(530f, 20f), ink, FontStyle.Bold);
            var desc = RunHudUiFactory.CreateText(panel.rectTransform, "Goal", quest.Description, 13, TextAnchor.MiddleLeft,
                ColonistUiKit.InkMuted, FontStyle.Normal);
            desc.resizeTextForBestFit = true;
            desc.resizeTextMinSize = 9;
            desc.resizeTextMaxSize = 13;
            SetRect(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f + 200f, -6f), new Vector2(400f, 30f));
            // Progress bar + count on the right.
            const float barW = 104f;
            var track = CreateImage(panel.rectTransform, "Track", null);
            track.color = new Color(0f, 0f, 0f, 0.18f);
            SetRect(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(-30f - barW / 2f, -16f), new Vector2(barW, 10f));
            var fill = CreateImage(track.rectTransform, "Fill", null);
            fill.color = me.QuestDone ? new Color(0.35f, 0.7f, 0.3f, 1f) : new Color(0.93f, 0.72f, 0.22f, 1f);
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2((float)progress / target, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            HsText(panel.rectTransform, "Progress", me.QuestDone ? "✓ done" : $"{progress} / {quest.Target}", 16,
                new Vector2(1f, 0.5f), new Vector2(-30f - barW / 2f, 4f), new Vector2(barW, 22f),
                me.QuestDone ? ColonistUiKit.InkGood : ink, FontStyle.Bold);
        }

        // ---------- trade countdown ----------

        private void BuildTradeCountdown(RectTransform card)
        {
            const float w = 260f;
            var holder = CreateImage(card, TradeCountdownName, null);
            holder.color = new Color(0f, 0f, 0f, 0.16f);
            holder.raycastTarget = false;
            SetRect(holder.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(w, 20f));
            var fill = CreateImage(holder.rectTransform, "Fill", null);
            fill.color = new Color(0.93f, 0.72f, 0.22f, 1f);
            fill.raycastTarget = false;
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(w, 0f);
            var text = RunHudUiFactory.CreateText(holder.rectTransform, "Seconds", "", 15, TextAnchor.MiddleCenter,
                ColonistUiKit.Ink, FontStyle.Bold);
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            _hsTradeCountdownText = text;
            _hsTradeCountdownFill = frt;
            _hsTradeCountdownWidth = w;
            UpdateTradeCountdown();
        }

        private void UpdateTradeCountdown()
        {
            if (_hsTradeCountdownText == null || _hotSeat == null)
                return;
            float left = _hotSeat.TradeSecondsLeft;
            float frac = Mathf.Clamp01(left / HotSeatSession.TradeOpenSeconds);
            _hsTradeCountdownText.text = $"Closes in {Mathf.CeilToInt(left)} s";
            _hsTradeCountdownFill.sizeDelta = new Vector2(_hsTradeCountdownWidth * frac, 0f);
            bool urgent = left <= 5f;
            var img = _hsTradeCountdownFill.GetComponent<Image>();
            img.color = urgent
                ? Color.Lerp(TimerPulseRed, new Color(1f, 0.55f, 0.4f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f))
                : new Color(0.93f, 0.72f, 0.22f, 1f);
            float s = urgent ? 1f + 0.06f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            _hsTradeCountdownText.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        // ---------- motion ----------

        /// <summary>After every rebuild: stamps panels that just appeared so they fade/slide in.</summary>
        private void NoteHotSeatPanels()
        {
            if (_hotSeatRoot == null)
                return;
            var seen = new HashSet<string>();
            float now = HudMotion.Now;
            foreach (Transform child in _hotSeatRoot)
            {
                seen.Add(child.name);
                if (!_hsPanelBorn.ContainsKey(child.name))
                    _hsPanelBorn[child.name] = _hsPanelsPrimed ? now : -100f;
            }

            var gone = new List<string>();
            foreach (var k in _hsPanelBorn.Keys)
                if (!seen.Contains(k))
                    gone.Add(k);
            foreach (var k in gone)
                _hsPanelBorn.Remove(k);
            _hsPanelsPrimed = true;

            var state = _hotSeat.State;
            int n = state.Seats.Count;
            if (_hsVpLast == null || _hsVpLast.Length != n)
            {
                _hsVpLast = new int[n];
                _hsVpChangedAt = new float[n];
                for (int i = 0; i < n; i++)
                {
                    _hsVpLast[i] = state.Seats[i].PublicVictoryPoints;
                    _hsVpChangedAt[i] = -100f;
                }
            }

            for (int i = 0; i < n; i++)
            {
                int vp = state.Seats[i].PublicVictoryPoints;
                if (vp != _hsVpLast[i])
                    _hsVpChangedAt[i] = now;
                _hsVpLast[i] = vp;
            }

            UpdateHotSeatMotion();
        }

        /// <summary>Every frame: panel fade/slide-ins, the active seat's pulse, VP punches, the trade countdown.</summary>
        private void UpdateHotSeatMotion()
        {
            if (_hotSeat == null || _hotSeatRoot == null)
                return;
            float now = HudMotion.Now;
            foreach (Transform child in _hotSeatRoot)
            {
                if (!_hsPanelBorn.TryGetValue(child.name, out float born))
                    continue;
                float t = Mathf.Clamp01((now - born) / PanelInSeconds);
                var group = child.GetComponent<CanvasGroup>();
                if (t >= 1f)
                {
                    if (group != null && group.alpha < 1f)
                    {
                        group.alpha = 1f;
                        child.localScale = Vector3.one;
                    }

                    continue;
                }

                if (group == null)
                    group = child.gameObject.AddComponent<CanvasGroup>();
                float e = SimpleTweens.EaseOutQuad(t);
                group.alpha = e;
                float sc = Mathf.Lerp(0.94f, 1f, SimpleTweens.EaseOutBack(t));
                child.localScale = new Vector3(sc, sc, 1f);
            }

            var state = _hotSeat.State;
            if (_hsSeatRows != null)
            {
                for (int i = 0; i < _hsSeatRows.Length; i++)
                {
                    var row = _hsSeatRows[i];
                    if (row == null)
                        continue;
                    bool active = i == state.CurrentSeat && !_hotSeat.IsOver;
                    if (active)
                    {
                        float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
                        row.color = new Color(1f, 0.82f, 0.36f, Mathf.Lerp(0.42f, 0.72f, p));
                    }
                }
            }

            if (_hsVpTexts != null && _hsVpChangedAt != null)
            {
                for (int i = 0; i < _hsVpTexts.Length && i < _hsVpChangedAt.Length; i++)
                {
                    var vp = _hsVpTexts[i];
                    if (vp == null)
                        continue;
                    float t = (now - _hsVpChangedAt[i]) / VpPunchSeconds;
                    if (t >= 0f && t < 1f)
                    {
                        float s = SimpleTweens.PunchScale(t, 0.55f);
                        vp.rectTransform.localScale = new Vector3(s, s, 1f);
                        vp.color = Color.Lerp(new Color(0.95f, 0.62f, 0.12f, 1f), ColonistUiKit.TextOn(UiKitPiece.FrameMedium), t);
                    }
                    else if (vp.rectTransform.localScale != Vector3.one)
                    {
                        vp.rectTransform.localScale = Vector3.one;
                        vp.color = ColonistUiKit.TextOn(UiKitPiece.FrameMedium);
                    }
                }
            }

            UpdateTradeCountdown();
        }

        /// <summary>Captures: finish every HUD and board animation so the frame is sharp.</summary>
        internal void SnapMotionForCapture()
        {
            HudMotion.Snap();
            UpdateHotSeatMotion();
            var boardView = Object.FindFirstObjectByType<BoardView>();
            if (boardView != null)
                boardView.ApplyMotion();
        }
    }

    /// <summary>v146: pointer enter/exit callbacks (the trait chip's hover tooltip).</summary>
    public sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action Enter;
        public System.Action Exit;

        public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
    }

    /// <summary>v146: buttons lean in on hover and squash on press (scale only, eased, unscaled time).</summary>
    public sealed class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        private const float Hover = 1.05f, Press = 0.93f, Speed = 14f;
        private float _current = 1f, _target = 1f;
        private bool _over;
        private Vector3 _rest = Vector3.one;
        private Selectable _sel;

        public float CurrentScaleForTests => _current;

        private void Awake() => _sel = GetComponent<Selectable>();

        private bool Live => _sel == null || _sel.IsInteractable();

        private void Begin(float target)
        {
            if (Mathf.Approximately(_current, 1f))
                _rest = transform.localScale;
            _target = Live ? target : 1f;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _over = true;
            Begin(Hover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            Begin(1f);
        }

        public void OnPointerDown(PointerEventData eventData) => Begin(Press);
        public void OnPointerUp(PointerEventData eventData) => Begin(_over ? Hover : 1f);

        private void Update()
        {
            if (Mathf.Approximately(_current, _target))
                return;
            _current = Mathf.MoveTowards(_current, _target, Time.unscaledDeltaTime * Speed * 0.25f);
            if (Mathf.Abs(_current - _target) < 0.002f)
                _current = _target;
            transform.localScale = _rest * _current;
        }

        private void OnDisable()
        {
            if (!Mathf.Approximately(_current, 1f))
                transform.localScale = _rest;
            _current = _target = 1f;
        }
    }
}
