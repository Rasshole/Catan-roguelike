using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Victory;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v132: painted-card versions of the level-up perk choice and the end-of-run screen (they were plain
    /// buttons in the action bar). Same parchment/wood card language as the civilization pick.
    /// </summary>
    public sealed partial class RunHudController
    {
        private RectTransform _levelUpRoot;
        private string _levelUpKey;
        private RectTransform _runEndRoot;
        private string _runEndKey;

        public bool IsLevelUpOverlayVisibleForTests => _levelUpRoot != null && _levelUpRoot.gameObject.activeSelf;
        public bool IsRunEndOverlayVisibleForTests => _runEndRoot != null && _runEndRoot.gameObject.activeSelf;

        private void RefreshRewardOverlays(GameState state)
        {
            if (_canvas == null || !ColonistPresentation.IsActive)
                return;
            RefreshLevelUpOverlay(state);
            RefreshRunEndOverlay(state);
        }

        // ---------- level-up perks ----------

        private void RefreshLevelUpOverlay(GameState state)
        {
            bool show = state.Phase == GamePhase.LevelUpChoice && state.PendingLevelUpChoices.Count > 0
                                                               && !state.Winner.HasValue;
            if (!show)
            {
                if (_levelUpRoot != null)
                    _levelUpRoot.gameObject.SetActive(false);
                return;
            }

            string key = string.Join(",", state.PendingLevelUpChoices);
            if (_levelUpRoot == null || _levelUpKey != key)
            {
                if (_levelUpRoot != null)
                    ColonistSceneDestroy.SafeDestroy(_levelUpRoot.gameObject);
                _levelUpRoot = BuildLevelUp(state);
                _levelUpKey = key;
            }

            _levelUpRoot.gameObject.SetActive(true);
            _levelUpRoot.SetAsLastSibling();
        }

        public static string PerkDisplayName(LevelUpPerkId perk)
        {
            var sb = new StringBuilder();
            string raw = perk.ToString();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(raw[i]);
            }

            return sb.ToString();
        }

        private RectTransform BuildLevelUp(GameState state)
        {
            var root = CreateFullscreenDim("LevelUp", new Color(0.05f, 0.04f, 0.03f, 0.72f));
            string leader = LeaderLibrary.All.TryGetValue(state.Leader, out var def) ? def.Name : "your leader";
            // v134: the civ's named leader when a civilization was chosen (civs and leaders are one concept).
            if (Core.Civilizations.CivilizationLibrary.TryGet(state.Civilization, out var civDef)
                && !string.IsNullOrEmpty(civDef.LeaderName))
                leader = civDef.LeaderName;

            var title = RunHudUiFactory.CreateText(root, "Title", "Level up!", 46, TextAnchor.MiddleCenter, TitleCream,
                FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 64f));
            AddShadow(title.gameObject, 2f);
            var subtitle = RunHudUiFactory.CreateText(root, "Subtitle", $"Choose a perk for {leader}.", 21,
                TextAnchor.MiddleCenter, new Color(0.90f, 0.84f, 0.70f, 1f), FontStyle.Italic);
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1200f, 32f));
            AddShadow(subtitle.gameObject, 1.5f);

            // v133: the leader's medallion beside the title.
            var portraitSprite = state.Civilization != Core.Civilizations.CivilizationId.None
                ? ColonistRoguelikeArt.GetCivilizationPortrait(state.Civilization)
                : ColonistRoguelikeArt.GetLeaderPortrait(state.Leader);
            if (portraitSprite != null)
            {
                var portrait = CreateImage(root, "LeaderPortrait", portraitSprite);
                SetRect(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(-230f, -172f),
                    new Vector2(118f, 118f));
                AddShadow(portrait.gameObject, 2f);
            }

            var perks = state.PendingLevelUpChoices.ToList();
            const float cardW = 300f, cardH = 420f, gap = 40f;
            float total = perks.Count * cardW + (perks.Count - 1) * gap;
            for (int i = 0; i < perks.Count; i++)
            {
                float x = -total / 2f + cardW / 2f + i * (cardW + gap);
                BuildPerkCard(root, perks[i], new Vector2(x, -60f), new Vector2(cardW, cardH));
            }

            return root;
        }

        private void BuildPerkCard(RectTransform parent, LevelUpPerkId perk, Vector2 pos, Vector2 size)
        {
            var card = CreateCardFrame(parent, "Perk_" + perk, wide: false);
            SetRect(card, new Vector2(0.5f, 0.5f), pos, size);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
            colors.pressedColor = new Color(0.86f, 0.80f, 0.70f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (_controller == null)
                    return;
                _controller.ChooseLevelUpPerk(perk);
                Refresh(_controller.State);
            });

            var kicker = RunHudUiFactory.CreateText(card, "Kicker", "PERK", 14, TextAnchor.MiddleCenter,
                ColonistUiKit.InkGold, FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(size.x - 80f, 20f));

            var name = RunHudUiFactory.CreateText(card, "Name", PerkDisplayName(perk), 26, TextAnchor.MiddleCenter,
                InkBrown, FontStyle.Bold);
            SetRect(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(size.x - 76f, 70f));

            var rule = CreateImage(card, "Rule", null);
            rule.color = ColonistUiKit.Rule;
            SetRect(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(size.x - 120f, 2f));

            // Centred in the space between the rule and the hint so short perks don't leave a gap below.
            var body = RunHudUiFactory.CreateText(card, "Description", LevelUpLibrary.GetDescription(perk), 20,
                TextAnchor.MiddleCenter, InkGood, FontStyle.Bold);
            SetRect(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(size.x - 80f, 140f));

            var hint = RunHudUiFactory.CreateText(card, "Hint", "click to choose", 13, TextAnchor.MiddleCenter,
                InkMuted, FontStyle.Italic);
            SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(size.x - 80f, 20f));
        }

        // ---------- end of run ----------

        private void RefreshRunEndOverlay(GameState state)
        {
            if (!state.Winner.HasValue)
            {
                if (_runEndRoot != null)
                    _runEndRoot.gameObject.SetActive(false);
                return;
            }

            string key = $"{state.Winner}:{state.PlayerVictoryPoints}:{state.AiVictoryPoints}:{_lastRunStarsEarned}";
            if (_runEndRoot == null || _runEndKey != key)
            {
                if (_runEndRoot != null)
                    ColonistSceneDestroy.SafeDestroy(_runEndRoot.gameObject);
                _runEndRoot = BuildRunEnd(state);
                _runEndKey = key;
            }

            _runEndRoot.gameObject.SetActive(true);
            _runEndRoot.SetAsLastSibling();
        }

        private RectTransform BuildRunEnd(GameState state)
        {
            bool won = state.Winner == PlayerId.Human;
            var root = CreateFullscreenDim("RunEnd", new Color(0.05f, 0.04f, 0.03f, 0.72f));

            const float w = 760f, h = 440f;
            var card = CreateCardFrame(root, "Card", wide: true);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(w, h));
            var cardImage = card.GetComponent<Image>();
            var sliced = ColonistRoguelikeArt.GetCardFrameSliced(true);
            if (sliced != null)
            {
                cardImage.sprite = sliced;
                cardImage.type = Image.Type.Sliced;
                cardImage.preserveAspect = false;
                cardImage.pixelsPerUnitMultiplier = sliced.texture.width / w;
            }

            // v134: the civ's face is its leader portrait; the emblem only shows separately when a real portrait exists
            // (otherwise the emblem itself stands in as the portrait).
            bool hasCiv = state.Civilization != Core.Civilizations.CivilizationId.None;
            bool hasEmblem = hasCiv && ColonistRoguelikeArt.HasCivilizationPortrait(state.Civilization);
            var leaderPortrait = hasCiv
                ? ColonistRoguelikeArt.GetCivilizationPortrait(state.Civilization)
                : ColonistRoguelikeArt.GetLeaderPortrait(state.Leader);
            bool hasArt = leaderPortrait != null;
            float textX = hasArt ? 250f : 80f;
            float textW = w - textX - 80f;
            if (leaderPortrait != null)
            {
                // v133/v134: the run's leader medallion (= the civilization's face), emblem as a small crest.
                var portrait = CreateImage(card, "LeaderPortrait", leaderPortrait);
                SetRect(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(150f, 18f), new Vector2(170f, 170f));
                if (hasEmblem)
                {
                    var crest = CreateImage(card, "Emblem",
                        ColonistRoguelikeArt.GetCivilizationEmblem(state.Civilization));
                    SetRect(crest.rectTransform, new Vector2(0f, 0.5f), new Vector2(214f, -52f), new Vector2(64f, 64f));
                }

                var ps = portrait.gameObject.AddComponent<Shadow>();
                ps.effectColor = new Color(0.20f, 0.12f, 0.05f, 0.45f);
                ps.effectDistance = new Vector2(2f, -3f);
            }

            Text Line(string name, string text, int size, Color color, FontStyle style, float top, float height)
            {
                var t = RunHudUiFactory.CreateText(card, name, text, size, TextAnchor.UpperLeft, color, style);
                SetRect(t.rectTransform, new Vector2(0f, 1f), new Vector2(textX + textW / 2f, -top - height / 2f),
                    new Vector2(textW, height));
                return t;
            }

            Line("Kicker", $"RUN COMPLETE  ·  DAY {state.Board.DayNumber}", 14, ColonistUiKit.InkGold, FontStyle.Bold,
                66f, 20f);
            Line("Title", won ? "Victory!" : "Defeat", 46, won ? InkGood : InkBad, FontStyle.Bold, 88f, 56f);
            Line("Score", $"You {state.PlayerVictoryPoints} VP   ·   Opponent {state.AiVictoryPoints} VP", 22, InkBrown,
                FontStyle.Bold, 152f, 30f);

            string summary = "";
            if (RunSummaryDisplay.TryGetSummaryLines(state, _controller != null ? _controller.RunSeed : 0,
                    out var lines) && lines.Count > 1)
                summary = string.Join("\n", lines.Skip(1).Take(2));
            if (string.IsNullOrEmpty(summary))
                summary = VictoryCalculator.GetBreakdown(state, PlayerId.Human).FormatLine();
            Line("Summary", summary, 16, InkMuted, FontStyle.Normal, 190f, 60f);

            if (_lastRunStarsEarned > 0)
                Line("Stars", $"+{_lastRunStarsEarned} ★ earned for your next run", 19, ColonistUiKit.InkGold,
                    FontStyle.Bold, 254f, 26f);

            var restart = RunHudUiFactory.CreateButton(card, ColonistHudCopy.Restart, RunHudUiFactory.ButtonFill);
            var rr = (RectTransform)restart.transform;
            SetRect(rr, new Vector2(0f, 0f), new Vector2(textX + 120f, 92f), new Vector2(240f, 54f));
            ColonistKenneyHudArt.ApplyColonistWarmButton(restart.GetComponent<Image>(), primaryGreen: true);
            var label = restart.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = ColonistUiKit.Cream;
                label.fontSize = 20;
                label.fontStyle = FontStyle.Bold;
            }

            restart.onClick.AddListener(() =>
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            return root;
        }
    }
}
