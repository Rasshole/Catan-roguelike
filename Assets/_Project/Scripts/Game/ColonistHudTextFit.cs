using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Guards colonist right-rail labels against horizontal overflow (legacy uGUI Text).</summary>
    public static class ColonistHudTextFit
    {
        public const float WidthSlackPixels = 2f;

        public static bool PreferredWidthFits(Text text)
        {
            if (text == null)
                return true;

            Canvas.ForceUpdateCanvases();
            var rect = text.rectTransform;
            if (rect.rect.width <= 1f)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            float maxWidth = rect.rect.width;
            if (maxWidth <= 1f)
                return true;

            return text.preferredWidth <= maxWidth + WidthSlackPixels;
        }

        public static void CollectColonistRailHeaderTexts(Transform canvasRoot, List<Text> sink)
        {
            if (canvasRoot == null || sink == null)
                return;

            var rightRail = canvasRoot.Find("RightRail");
            if (rightRail == null)
                return;

            TryAddTitle(rightRail, "GameLogPanel", sink);
            TryAddTitle(rightRail, "ChatPanel", sink);
            TryAddBody(rightRail, "PlayerYou", sink);
            TryAddBody(rightRail, "PlayerAi", sink);
        }

        public static void AssertAllColonistRailTextsFit(Transform canvasRoot)
        {
            var texts = new List<Text>();
            CollectColonistRailHeaderTexts(canvasRoot, texts);
            Canvas.ForceUpdateCanvases();

            foreach (var text in texts)
            {
                if (!PreferredWidthFits(text))
                {
                    throw new UnityEngine.Assertions.AssertionException(
                        $"Colonist rail text '{text.name}' preferredWidth={text.preferredWidth:F1} exceeds rect width={text.rectTransform.rect.width:F1} (text='{text.text}')",
                        text.gameObject);
                }
            }
        }

        private static void TryAddTitle(Transform rightRail, string panelName, List<Text> sink)
        {
            var panel = rightRail.Find(panelName);
            if (panel == null)
                return;

            var title = panel.Find("Title")?.GetComponent<Text>();
            if (title != null)
                sink.Add(title);
        }

        private static void TryAddBody(Transform rightRail, string panelName, List<Text> sink)
        {
            var panel = rightRail.Find(panelName);
            var body = panel?.Find("Row/Body/Text")?.GetComponent<Text>();
            if (body != null)
                sink.Add(body);
        }
    }
}
