using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v138: civilization info lives in a popup instead of an always-visible panel. Click a leader portrait in the
    /// player list (pass-and-play seat strip, or your emblem on the single-player rail) and a painted parchment card
    /// opens beside the list: civ, leader portrait, passive, drawback and trait. The secret quest (and its VP) — and
    /// in pass-and-play the secret trait — only show on the card of the seat whose private view is open; other
    /// seats' secrets never render (until the game-over reveal). Closes on a second click on the same portrait, a
    /// click outside, or the ✕.
    /// </summary>
    public sealed partial class RunHudController
    {
        private int _hsInfoSeat = -1;
        private int _hsInfoViewer = -1;
        private bool _spCivInfoOpen;
        private RectTransform _spCivInfoRoot;
        private Button _spCivEmblemButton;

        private sealed class CivInfoContent
        {
            public string Kicker;
            public string Title;
            public Color SeatColor;
            public Sprite Portrait;
            public string CivName;
            public string LeaderLine;
            public string Passive;
            public string Drawback;
            public readonly List<(string name, string head, string body, Color color)> Sections = new();
            public string Footer;
        }

        // ---------- pass-and-play ----------

        /// <summary>The seat whose private view is on screen (its hand is visible), or -1 (pass screen / AI turn).</summary>
        public int HotSeatViewerSeat =>
            _hotSeat != null && _hotSeat.IsHandVisible(_hotSeat.State.ActingSeat) ? _hotSeat.State.ActingSeat : -1;

        public int HotSeatInfoSeatForTests => _hsInfoSeat;

        public RectTransform HotSeatInfoPopupForTests => _hotSeatRoot == null ? null : _hotSeatRoot.Find("CivInfoPopup") as RectTransform;

        /// <summary>Tests: same path as clicking a seat's portrait in the strip.</summary>
        public void ClickHotSeatPortraitForTests(int seat)
        {
            var b = _hotSeatRoot == null ? null : _hotSeatRoot.Find($"SeatStrip/Seat_{seat}/Civ")?.GetComponent<Button>();
            if (b != null)
                b.onClick.Invoke();
        }

        /// <summary>Tests: a click on the dimmed area outside the popup.</summary>
        public void ClickOutsideHotSeatInfoForTests()
        {
            var b = _hotSeatRoot == null ? null : _hotSeatRoot.Find("CivInfoCatcher")?.GetComponent<Button>();
            if (b != null)
                b.onClick.Invoke();
        }

        private void ToggleHotSeatInfo(int seat)
        {
            _hsInfoSeat = _hsInfoSeat == seat ? -1 : seat;
            _hsInfoViewer = HotSeatViewerSeat;
            HsMark();
        }

        private void MakeSeatPortraitClickable(Image face, int seat)
        {
            face.raycastTarget = true;
            var b = face.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.82f, 1f);
            colors.pressedColor = new Color(0.85f, 0.80f, 0.70f, 1f);
            b.colors = colors;
            b.onClick.AddListener(() => ToggleHotSeatInfo(seat));
        }

        private void BuildHotSeatInfoPopup(MatchState state)
        {
            var s = _hotSeat;
            // Privacy: a pass screen or a change of who holds the device closes the card.
            if (_hsInfoSeat >= 0 && (s.PassScreenPending || !state.Config.PartyRules || _hsInfoSeat >= state.Seats.Count
                                     || HotSeatViewerSeat != _hsInfoViewer && !s.IsOver
                                     || !SeatCivRevealed(state, state.Seats[_hsInfoSeat])))
                _hsInfoSeat = -1;
            if (_hsInfoSeat < 0)
                return;

            int seatIndex = _hsInfoSeat;
            var seat = state.Seats[seatIndex];
            bool own = seatIndex == HotSeatViewerSeat;
            bool reveal = own || s.IsOver;
            var content = new CivInfoContent
            {
                Kicker = own ? $"SEAT {seatIndex + 1}  ·  YOU" : $"SEAT {seatIndex + 1}" + (seat.IsAi ? "  ·  AI" : ""),
                Title = seat.Name,
                SeatColor = PlayerPieceVisuals.ColorForSeat(seatIndex),
                Portrait = ColonistRoguelikeArt.GetCivilizationPortrait(seat.Civilization),
                Passive = MatchCivRules.Passive(seat.Civilization),
                Drawback = MatchCivRules.Drawback(seat.Civilization)
            };
            if (CivilizationLibrary.TryGet(seat.Civilization, out var def))
            {
                content.CivName = def.Name;
                content.LeaderLine = string.IsNullOrEmpty(def.LeaderName) ? null : "led by " + def.LeaderName;
            }

            // v146: traits are public (everyone sees every seat's trait); only the quest stays secret.
            var trait = MatchPartyLibrary.Trait(seat.Trait);
            content.Sections.Add(("Trait", "TRAIT" + (trait != null ? "  ·  " + trait.Name.ToUpperInvariant() : ""),
                trait != null ? trait.Description : "Picked after placement.", trait != null ? ColonistUiKit.Ink : ColonistUiKit.InkMuted));
            if (reveal)
            {
                var quest = MatchPartyLibrary.Quest(seat.Quest);
                if (quest != null)
                {
                    string progress = seat.QuestDone ? "✓ done" : $"{quest.CurrentProgress(s.Match, seatIndex)} / {quest.Target}";
                    content.Sections.Add(("Quest",
                        $"SECRET QUEST  ·  {quest.Points} VP  ·  {progress}",
                        $"{quest.Name}: {quest.Description}",
                        seat.QuestDone ? ColonistUiKit.InkGood : ColonistUiKit.Ink));
                }
                else
                    content.Sections.Add(("Quest", "SECRET QUEST", "Picked after placement.", ColonistUiKit.InkMuted));
            }
            else
                content.Footer = "Their secret quest stays hidden until the game ends.";

            // Click-outside catcher (transparent), then the strip above it so another portrait switches cards.
            var catcher = CreateImage(_hotSeatRoot, "CivInfoCatcher", null);
            catcher.color = new Color(0f, 0f, 0f, 0.001f);
            catcher.raycastTarget = true;
            Stretch(catcher.rectTransform);
            var cb = catcher.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None;
            cb.onClick.AddListener(() =>
            {
                _hsInfoSeat = -1;
                HsMark();
            });
            _hotSeatRoot.Find("SeatStrip")?.SetAsLastSibling();

            var card = BuildCivInfoCard(_hotSeatRoot, "CivInfoPopup", content, 470f, () =>
            {
                _hsInfoSeat = -1;
                HsMark();
            });
            // Beside the strip (right edge 20 px in, 360 wide), top aligned with the clicked row.
            float h = card.sizeDelta.y;
            float rowCentre = -24f - 64f - 52f - seatIndex * 104f;
            float top = Mathf.Max(rowCentre + 60f, -1060f + h);
            top = Mathf.Min(top, -24f);
            SetRect(card, new Vector2(1f, 1f), new Vector2(-20f - 360f - 14f - 235f, top - h / 2f), card.sizeDelta);
            // A small pointer toward the clicked row.
            var tip = CreateImage(card, "Pointer", DiscSprite());
            tip.color = PlayerPieceVisuals.ColorForSeat(seatIndex);
            SetRect(tip.rectTransform, new Vector2(1f, 1f), new Vector2(4f, rowCentre - top), new Vector2(18f, 18f));
        }

        // ---------- single player ----------

        public bool IsCivInfoPopupVisibleForTests => _spCivInfoRoot != null && _spCivInfoRoot.gameObject.activeInHierarchy;

        public RectTransform CivInfoPopupForTests => _spCivInfoRoot;

        /// <summary>Tests: same path as clicking your emblem on the rail.</summary>
        public void ClickOwnCivEmblemForTests()
        {
            if (_spCivEmblemButton != null)
                _spCivEmblemButton.onClick.Invoke();
        }

        private void ToggleSinglePlayerCivInfo()
        {
            _spCivInfoOpen = !_spCivInfoOpen;
            if (_controller != null)
                RefreshSinglePlayerCivInfo(_controller.State);
        }

        private void RefreshSinglePlayerCivInfo(GameState state)
        {
            bool hasCiv = state != null && CivilizationLibrary.TryGet(state.Civilization, out _);
            if (!hasCiv || state.Winner.HasValue)
                _spCivInfoOpen = false;
            if (_spCivInfoRoot != null)
            {
                ColonistSceneDestroy.SafeDestroy(_spCivInfoRoot.gameObject);
                _spCivInfoRoot = null;
            }

            if (!_spCivInfoOpen || _canvas == null)
                return;

            CivilizationLibrary.TryGet(state.Civilization, out var def);
            var content = new CivInfoContent
            {
                Kicker = "YOUR CIVILIZATION",
                Title = ColonistHudCopy.You,
                SeatColor = new Color(0.22f, 0.55f, 0.95f, 1f),
                Portrait = ColonistRoguelikeArt.GetCivilizationPortrait(state.Civilization),
                CivName = def.Name,
                LeaderLine = string.IsNullOrEmpty(def.LeaderName) ? null : "led by " + def.LeaderName,
                Passive = def.Passive,
                Drawback = def.Drawback
            };
            content.Sections.Add(("Leader", "LEADER  ·  " + def.LeaderTitle.ToUpperInvariant(), def.LeaderAbility,
                ColonistUiKit.Ink));

            var root = new GameObject("CivInfoLayer").AddComponent<RectTransform>();
            root.SetParent(_canvas.transform, false);
            Stretch(root);
            var catcher = CreateImage(root, "CivInfoCatcher", null);
            catcher.color = new Color(0f, 0f, 0f, 0.001f);
            catcher.raycastTarget = true;
            Stretch(catcher.rectTransform);
            var cb = catcher.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None;
            cb.onClick.AddListener(ToggleSinglePlayerCivInfo);
            var card = BuildCivInfoCard(root, "CivInfoPopup", content, 470f, ToggleSinglePlayerCivInfo);
            float right = RunHudLayout.GetContentRightInset();
            SetRect(card, new Vector2(1f, 1f), new Vector2(-right - 12f - 235f, -40f - card.sizeDelta.y / 2f),
                card.sizeDelta);
            SetLayerRecursively(root.gameObject, HudCaptureUiLayer);
            root.SetAsLastSibling();
            _spCivInfoRoot = root;
        }

        // ---------- the card ----------

        private static Text CivInfoText(RectTransform card, string name, string text, int size, Color color, FontStyle style,
            float x, float top, float width, TextAnchor align = TextAnchor.UpperLeft)
        {
            var t = RunHudUiFactory.CreateText(card, name, text, size, align, color, style);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(x, -top);
            t.rectTransform.sizeDelta = new Vector2(width, 10f);
            // Measure each paragraph on one unwrapped line and estimate the wrapped line count: preferredHeight with
            // Wrap under the freshly built, scaled canvas under-reports wrapped lines.
            var paragraphs = (text ?? "").Split('\n');
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float lineHeight = Mathf.Max(t.preferredHeight / paragraphs.Length, size * 1.15f);
            int lines = 0;
            foreach (var para in paragraphs)
            {
                t.text = para;
                lines += Mathf.Max(1, Mathf.CeilToInt(t.preferredWidth * 1.06f / width));
            }

            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            float h = Mathf.Ceil(lines * lineHeight) + 2f;
            t.rectTransform.sizeDelta = new Vector2(width, h);
            return t;
        }

        private static RectTransform BuildCivInfoCard(Transform parent, string name, CivInfoContent c, float width,
            System.Action close)
        {
            var img = CreateImage(parent, name, null);
            img.preserveAspect = false;
            img.raycastTarget = true; // clicks on the card don't fall through to the catcher
            ColonistUiKit.Apply(img, UiKitPiece.FrameMedium);
            var card = img.rectTransform;
            card.anchorMin = card.anchorMax = new Vector2(1f, 1f);
            card.sizeDelta = new Vector2(width, 400f);
            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(4f, -6f);

            const float pad = 34f, portrait = 120f;
            var ring = CreateDisc(card, "Ring", c.SeatColor, portrait + 12f);
            SetRect(ring.rectTransform, new Vector2(0f, 1f), new Vector2(pad + (portrait + 12f) / 2f, -pad - (portrait + 12f) / 2f),
                new Vector2(portrait + 12f, portrait + 12f));
            var face = CreateImage(card, "Portrait", c.Portrait);
            face.preserveAspect = true;
            SetRect(face.rectTransform, new Vector2(0f, 1f), ring.rectTransform.anchoredPosition, new Vector2(portrait, portrait));

            float tx = pad + portrait + 12f + 18f;
            float tw = width - tx - pad - 34f;
            float y = pad + 4f;
            y += CivInfoText(card, "Kicker", c.Kicker, 14, ColonistUiKit.InkGold, FontStyle.Bold, tx, y, tw).rectTransform.sizeDelta.y;
            y += CivInfoText(card, "Seat", c.Title, 24, ColonistUiKit.Ink, FontStyle.Bold, tx, y, tw).rectTransform.sizeDelta.y;
            if (!string.IsNullOrEmpty(c.CivName))
                y += CivInfoText(card, "CivName", c.CivName, 19, ColonistUiKit.Ink, FontStyle.Bold, tx, y, tw).rectTransform.sizeDelta.y;
            if (!string.IsNullOrEmpty(c.LeaderLine))
                y += CivInfoText(card, "Leader", c.LeaderLine, 15, ColonistUiKit.InkMuted, FontStyle.Italic, tx, y, tw)
                    .rectTransform.sizeDelta.y;

            TradeButton(card, "CivInfoClose", "", TradeGlyphSprites.Cross, new Vector2(1f, 1f),
                new Vector2(-pad - 12f, -pad - 10f), new Vector2(40f, 38f), true, false, close);

            y = Mathf.Max(y, pad + portrait + 12f) + 12f;
            float bw = width - 2f * pad;

            void Rule(string n)
            {
                var rule = CreateImage(card, n, null);
                rule.color = ColonistUiKit.Rule;
                rule.rectTransform.anchorMin = rule.rectTransform.anchorMax = new Vector2(0f, 1f);
                rule.rectTransform.pivot = new Vector2(0f, 1f);
                rule.rectTransform.anchoredPosition = new Vector2(pad, -y);
                rule.rectTransform.sizeDelta = new Vector2(bw, 2f);
                y += 10f;
            }

            Rule("Rule0");
            y += CivInfoText(card, "Passive", "✦ " + c.Passive, 18, ColonistUiKit.InkGood, FontStyle.Bold, pad, y, bw)
                .rectTransform.sizeDelta.y + 4f;
            bool hasDrawback = !string.IsNullOrEmpty(c.Drawback);
            y += CivInfoText(card, "Drawback", hasDrawback ? "▲ " + c.Drawback : "No drawback", 16,
                hasDrawback ? ColonistUiKit.InkBad : ColonistUiKit.InkMuted, FontStyle.Italic, pad, y, bw)
                .rectTransform.sizeDelta.y + 8f;

            foreach (var (n, head, body, color) in c.Sections)
            {
                Rule(n + "Rule");
                y += CivInfoText(card, n + "Head", head, 14, ColonistUiKit.InkGold, FontStyle.Bold, pad, y, bw)
                    .rectTransform.sizeDelta.y + 2f;
                y += CivInfoText(card, n, body, 17, color, FontStyle.Normal, pad, y, bw).rectTransform.sizeDelta.y + 8f;
            }

            if (!string.IsNullOrEmpty(c.Footer))
            {
                Rule("FooterRule");
                y += CivInfoText(card, "Hidden", c.Footer, 15, ColonistUiKit.InkMuted, FontStyle.Italic, pad, y, bw,
                    TextAnchor.UpperCenter).rectTransform.sizeDelta.y + 4f;
            }

            card.sizeDelta = new Vector2(width, y + pad);
            return card;
        }
    }
}
