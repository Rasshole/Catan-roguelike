using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Authoritative build-button title for HUD tests and UI sync.</summary>
    public sealed class ColonistBuildButtonLabel : MonoBehaviour
    {
        public string TitleString { get; private set; } = string.Empty;

        public void SetTitle(string title)
        {
            TitleString = title ?? string.Empty;
            var label = transform.Find("TitleRow/Title")?.GetComponent<Text>();
            if (label != null)
                label.text = TitleString;
        }

        public static string GetTitle(Button button)
        {
            if (button == null)
                return string.Empty;

            var model = button.GetComponent<ColonistBuildButtonLabel>();
            if (model != null && !string.IsNullOrEmpty(model.TitleString))
                return model.TitleString;

            return ColonistBuildButtonChrome.GetTitleText(button);
        }

        public static string DescribeAllTexts(Button button)
        {
            if (button == null)
                return "(null button)";

            var lines = new System.Text.StringBuilder();
            lines.Append($"button={button.name} active={button.gameObject.activeInHierarchy}");
            foreach (var text in button.GetComponentsInChildren<Text>(true))
                lines.Append($" | Text[{text.gameObject.name}]='{text.text}'");

            return lines.ToString();
        }
    }
}
